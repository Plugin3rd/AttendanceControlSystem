using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    public UsersController(AttendanceDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _db.Users.Select(u => new { u.Id, u.Username, u.Role, u.IsActive, u.IsLocked,
            u.LastActivityAt, u.CreatedAt }).AsNoTracking().ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
    {
        if (dto.Role != "Admin" && dto.Role != "Operator")
            return BadRequest(new { error = "نقش نامعتبر." });

        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return Conflict(new { error = "نام کاربری تکراری است." });

        var roleText = dto.Role?.Trim();

        if (string.IsNullOrWhiteSpace(roleText) ||
            !Enum.TryParse<UserRole>(roleText, true, out var parsedRole) ||
            !Enum.IsDefined(typeof(UserRole), parsedRole))
        {
            return BadRequest(new
            {
                message = "نقش کاربر نامعتبر است.",
                validRoles = Enum.GetNames<UserRole>()
            });
        }

        var user = new User { Username = dto.Username, Role = parsedRole };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, dto.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.Role });
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Username == User.Identity?.Name) return BadRequest(new { error = "خودتان را غیرفعال نمی‌توانید کنید." });
        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPatch("{id}/lock")]
    public async Task<IActionResult> Lock(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.IsLocked = !user.IsLocked;
        user.LockedAt = user.IsLocked ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsLocked });
    }

    [HttpPatch("{id}/password")]
    public async Task<IActionResult> Password(int id, [FromBody] PasswordDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, dto.Password);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPatch("{id}/role")]
    public async Task<IActionResult> Role(int id, [FromBody] UserCreateDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (dto.Role != "Admin" && dto.Role != "Operator")
            return BadRequest(new { error = "نقش نامعتبر." });

        if (!Enum.TryParse<UserRole>(dto.Role, true, out var parsedRole) ||
            !Enum.IsDefined(typeof(UserRole), parsedRole))
        {
            return BadRequest(new
            {
                message = "نقش کاربر نامعتبر است.",
                validRoles = Enum.GetNames<UserRole>()
            });
        }

        user.Role = parsedRole;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Role });
    }

    [HttpGet("{id}/activity")]
    public async Task<IActionResult> Activity(int id, [FromQuery] int count = 100)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        var logs = await _db.AuditLogs.Where(a => a.Username == user.Username)
            .OrderByDescending(a => a.AtUtc).Take(count).ToListAsync();
        return Ok(logs);
    }
}
