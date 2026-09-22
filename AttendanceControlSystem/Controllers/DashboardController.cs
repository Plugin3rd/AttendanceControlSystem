using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private const int MaximumListSize = 10;
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;

    public DashboardController(AttendanceDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var activeUnitIds = await _db.Units
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            ActivePeople = await _db.People.CountAsync(p => p.IsActive),
            ActiveUnits = await _db.Units.CountAsync(u => u.IsActive),
            TodayEntries = await _db.AttendanceRecords.CountAsync(r =>
                r.EntryAtUtc >= todayStart && r.EntryAtUtc < tomorrowStart),
            TodayExits = await _db.AttendanceRecords.CountAsync(r =>
                r.ExitAtUtc >= todayStart && r.ExitAtUtc < tomorrowStart),
            PresentCount = await _db.AttendanceRecords.CountAsync(r =>
                r.ExitAtUtc == null
                && r.Person.IsActive
                && r.UnitId.HasValue
                && activeUnitIds.Contains(r.UnitId.Value)),
            ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
            IsAdmin = User.IsInRole("Admin"),
            RecentEntries = await _db.AttendanceRecords
                .AsNoTracking()
                .Include(r => r.Person)
                .Include(r => r.Unit)
                .Include(r => r.RegisteredByUser)
                .OrderByDescending(r => r.EntryAtUtc)
                .ThenByDescending(r => r.Id)
                .Take(MaximumListSize)
                .ToListAsync(),
            PresentPeople = await _db.AttendanceRecords
                .AsNoTracking()
                .Include(r => r.Person)
                .Include(r => r.Unit)
                .Where(r => r.ExitAtUtc == null
                    && r.Person.IsActive
                    && r.UnitId.HasValue
                    && activeUnitIds.Contains(r.UnitId.Value))
                .OrderBy(r => r.EntryAtUtc)
                .ThenBy(r => r.Id)
                .Take(MaximumListSize)
                .ToListAsync(),
            ActiveUserList = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && !u.IsLocked)
                .OrderBy(u => u.Username)
                .Take(MaximumListSize)
                .ToListAsync()
        };

        ViewBag.TodayStart = todayStart;
        ViewBag.TodayEnd = tomorrowStart.AddTicks(-1);
        ViewBag.MaximumListSize = MaximumListSize;
        return View(vm);
    }
}
