using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly AuditService _audit;

    public AuthController(AttendanceDbContext db, JwtTokenService jwt, AuditService audit)
    { _db = db; _jwt = jwt; _audit = audit; }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user is null || !user.IsActive)
        {
            await _audit.LogAsync("LoginFailed", details: "bad username");
            return Unauthorized(new { error = "نام کاربری یا رمز عبور نادرست است." });
        }
        if (user.IsLocked)
            return Unauthorized(new { error = "حساب قفل شده است. برای بازگشایی از unlock استفاده کنید." });

        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
        {
            _db.AuditLogs.Add(new AuditLog { Username = dto.Username, Action = "LoginFailed", Details = "bad password" });
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "نام کاربری یا رمز عبور نادرست است." });
        }

        user.LastActivityAt = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog { Username = user.Username, Action = "Login" });
        await _db.SaveChangesAsync();
        var (token, expires) = _jwt.CreateTokenWithExpiry(user);
        return Ok(new { token, expiresUtc = expires, user.Username, role = user.Role });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            username = User.Identity?.Name,
            role = User.FindFirstValue(ClaimTypes.Role),
            id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "Logout" });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("unlock")]
    [AllowAnonymous]
    public async Task<IActionResult> Unlock([FromBody] UnlockDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
        var hasher = new PasswordHasher<User>();
        if (user is null || !user.IsLocked ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
        {
            await _audit.LogAsync("UnlockFailed");
            return Unauthorized(new { error = "بازگشایی ناموفق بود." });
        }
        user.IsLocked = false;
        user.LockedAt = null;
        user.LastActivityAt = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog { Username = user.Username, Action = "Unlock" });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
