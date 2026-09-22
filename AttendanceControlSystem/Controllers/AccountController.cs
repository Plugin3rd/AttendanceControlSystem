using System.Security.Claims;
using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Controllers;

public class AccountController : Controller
{
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;
    private readonly CookiePrincipalFactory _principalFactory;
    private readonly LoginThrottleService _loginThrottle;

    public AccountController(
        AttendanceDbContext db,
        AuditService audit,
        CookiePrincipalFactory principalFactory,
        LoginThrottleService loginThrottle)
    {
        _db = db;
        _audit = audit;
        _principalFactory = principalFactory;
        _loginThrottle = loginThrottle;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? r = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new AccountLoginViewModel { ReturnUrl = r });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AccountLoginViewModel vm, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var username = (vm.Username ?? string.Empty).Trim();
        var throttleKey = CreateThrottleKey(username);
        if (!_loginThrottle.CanAttempt(throttleKey, out var retryAfter))
        {
            var retrySeconds = retryAfter.HasValue
                ? Math.Max(1, (int)Math.Ceiling(retryAfter.Value.TotalSeconds))
                : 1;
            ModelState.AddModelError(
                string.Empty,
                $"تلاش‌های ورود بیش از حد مجاز است. لطفاً پس از {retrySeconds} ثانیه دوباره تلاش کنید.");
            await _audit.LogAsync(
                "LoginThrottled",
                details: "login throttle rejected the request",
                username: username,
                cancellationToken: cancellationToken);
            return View(vm);
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(candidate => candidate.Username == username, cancellationToken);
        if (user is null || !user.IsActive || user.IsLocked)
        {
            _loginThrottle.RecordFailure(throttleKey);
            await _audit.LogAsync(
                "LoginFailed",
                details: "account not found, inactive, or locked",
                username: username,
                cancellationToken: cancellationToken);
            AddInvalidCredentialsError();
            return View(vm);
        }

        var hasher = new PasswordHasher<User>();
        var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _loginThrottle.RecordFailure(throttleKey);
            await _audit.LogAsync(
                "LoginFailed",
                details: "invalid password",
                username: user.Username,
                userId: user.Id,
                cancellationToken: cancellationToken);
            AddInvalidCredentialsError();
            return View(vm);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, vm.Password);
        }

        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        var now = DateTime.UtcNow;
        user.LastLoginAtUtc = now;
        user.LastActivityAt = now;
        await _audit.LogAsync(
            "Login",
            entityName: nameof(User),
            details: user.Username,
            saveChanges: false,
            cancellationToken: cancellationToken,
            userId: user.Id,
            username: user.Username);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var principal = _principalFactory.Create(user);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = vm.RememberMe,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = vm.RememberMe
                    ? DateTimeOffset.UtcNow.AddHours(8)
                    : null,
                AllowRefresh = true
            });
        _loginThrottle.RecordSuccess(throttleKey);

        if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
        {
            return LocalRedirect(vm.ReturnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdValue, out var userId);
        var username = User.Identity?.Name;

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await using var transaction = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        await _audit.LogAsync(
            "Logout",
            entityName: nameof(User),
            details: username,
            saveChanges: false,
            cancellationToken: HttpContext.RequestAborted,
            userId: userId > 0 ? userId : null,
            username: username);
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);

        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private string CreateThrottleKey(string username)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{ipAddress}|{username.Trim().ToUpperInvariant()}";
    }

    private void AddInvalidCredentialsError()
    {
        ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور نادرست است.");
    }
}
