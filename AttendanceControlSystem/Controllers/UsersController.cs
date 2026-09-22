using System.Security.Claims;
using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private const int MaxUsernameLength = 50;
    private static readonly PasswordHasher<User> PasswordHasher = new();

    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;

    public UsersController(AttendanceDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.OrderBy(u => u.Username).ToListAsync();
        var currentUserId = GetCurrentUserId();
        var activeAdminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);

        ViewBag.CurrentUserId = currentUserId;
        ViewBag.ActiveAdminCount = activeAdminCount;
        return View(users);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel vm)
    {
        vm.Username = NormalizeUsername(vm.Username);
        ValidateUsername(vm.Username);
        if (string.IsNullOrWhiteSpace(vm.Password))
        {
            ModelState.AddModelError(nameof(vm.Password), "رمز عبور الزامی است.");
        }

        if (!Enum.IsDefined(typeof(UserRole), vm.Role))
        {
            ModelState.AddModelError(nameof(vm.Role), "نقش کاربر نامعتبر است.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        if (await IsUsernameTakenAsync(vm.Username))
        {
            ModelState.AddModelError(nameof(vm.Username), "نام کاربری قبلاً ثبت شده است.");
            return View(vm);
        }

        var user = new User
        {
            Username = vm.Username,
            PasswordHash = PasswordHasher.HashPassword(null!, vm.Password),
            Role = vm.Role,
            IsActive = vm.IsActive
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await IsUsernameTakenAsync(user.Username))
            {
                ModelState.AddModelError(nameof(vm.Username), "نام کاربری قبلاً ثبت شده است.");
                return View(vm);
            }

            throw;
        }

        await _audit.LogAsync("UserCreate", details: user.Username);
        TempData["Success"] = $"کاربر «{user.Username}» ایجاد شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var vm = new UserEditViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            IsActive = user.IsActive
        };

        ViewBag.IsCurrentUser = IsCurrentUser(user);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel vm)
    {
        vm.Username = NormalizeUsername(vm.Username);
        if (vm.Id <= 0)
        {
            ModelState.AddModelError(nameof(vm.Id), "شناسه کاربر نامعتبر است.");
        }

        ValidateUsername(vm.Username);

        if (!Enum.IsDefined(typeof(UserRole), vm.Role))
        {
            ModelState.AddModelError(nameof(vm.Role), "نقش کاربر نامعتبر است.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        if (await IsUsernameTakenAsync(vm.Username, vm.Id))
        {
            ModelState.AddModelError(nameof(vm.Username), "نام کاربری قبلاً ثبت شده است.");
        }

        var user = await _db.Users.FindAsync(vm.Id);
        if (user is null)
        {
            return NotFound();
        }

        var isCurrentUser = IsCurrentUser(user);
        ViewBag.IsCurrentUser = isCurrentUser;
        if (isCurrentUser && vm.Role != UserRole.Admin)
        {
            ModelState.AddModelError(
                nameof(vm.Role),
                "نمی‌توانید نقش کاربر جاری را از Admin تغییر دهید؛ نشست شما غیرفعال می‌شود.");
        }

        if (isCurrentUser && !vm.IsActive)
        {
            ModelState.AddModelError(
                nameof(vm.IsActive),
                "نمی‌توانید کاربر جاری را غیرفعال کنید؛ نشست شما پایان می‌یابد.");
        }

        if (user.Role == UserRole.Admin && user.IsActive &&
            (vm.Role != UserRole.Admin || !vm.IsActive) &&
            await IsLastActiveAdminAsync(user))
        {
            ModelState.AddModelError(
                string.Empty,
                "غیرفعال کردن یا تغییر نقش آخرین کاربر فعال Admin مجاز نیست.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var previousUsername = user.Username;
        user.Username = vm.Username;
        user.Role = vm.Role;
        user.IsActive = vm.IsActive;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await IsUsernameTakenAsync(user.Username, user.Id))
            {
                ModelState.AddModelError(nameof(vm.Username), "نام کاربری قبلاً ثبت شده است.");
                return View(vm);
            }

            throw;
        }

        await _audit.LogAsync(
            "UserEdit",
            details: previousUsername == user.Username
                ? $"تنظیمات کاربر «{user.Username}» ویرایش شد."
                : $"نام کاربری «{previousUsername}» به «{user.Username}» تغییر کرد.");
        TempData["Success"] = $"کاربر «{user.Username}» ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (IsCurrentUser(user))
        {
            TempData["Error"] = "غیرفعال کردن کاربر جاری مجاز نیست؛ نشست شما پایان می‌یابد.";
            return RedirectToAction(nameof(Index));
        }

        if (user.Role == UserRole.Admin && user.IsActive && await IsLastActiveAdminAsync(user))
        {
            TempData["Error"] = "غیرفعال کردن آخرین کاربر فعال Admin مجاز نیست.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UserDelete", details: user.Username);
        TempData["Success"] = $"کاربر «{user.Username}» غیرفعال شد.";
        return RedirectToAction(nameof(Index));
    }

    private static string NormalizeUsername(string? username)
    {
        return (username ?? string.Empty).Trim();
    }

    private void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            ModelState.AddModelError(nameof(UserCreateViewModel.Username), "نام کاربری الزامی است.");
        }
        else if (username.Length > MaxUsernameLength)
        {
            ModelState.AddModelError(
                nameof(UserCreateViewModel.Username),
                $"نام کاربری نمی‌تواند بیشتر از {MaxUsernameLength} کاراکتر باشد.");
        }
    }

    private async Task<bool> IsUsernameTakenAsync(string username, int? excludedUserId = null)
    {
        var query = _db.Users.AsQueryable();
        if (excludedUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludedUserId.Value);
        }

        return await query.AnyAsync(u => u.Username == username);
    }

    private async Task<bool> IsLastActiveAdminAsync(User user)
    {
        return await _db.Users.CountAsync(u =>
            u.Id != user.Id && u.Role == UserRole.Admin && u.IsActive) == 0;
    }

    private bool IsCurrentUser(User user)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue)
        {
            return currentUserId.Value == user.Id;
        }

        var currentUsername = User.Identity?.Name;
        return !string.IsNullOrWhiteSpace(currentUsername)
            && string.Equals(currentUsername, user.Username, StringComparison.Ordinal);
    }

    private int? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("UserId");

        return int.TryParse(userId, out var parsedUserId) ? parsedUserId : null;
    }
}
