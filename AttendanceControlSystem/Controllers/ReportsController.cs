using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private const int MaximumPageSize = 100;
    private readonly AttendanceDbContext _db;

    public ReportsController(AttendanceDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(DateTime? start, DateTime? end, int? unitId, int? status, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var startDate = start?.Date;
        var endDate = end?.Date;
        if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
        {
            ModelState.AddModelError(nameof(start), "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.");
        }

        if (status.HasValue && status.Value is not (0 or 1))
        {
            ModelState.AddModelError(nameof(status), "وضعیت گزارش معتبر نیست.");
        }

        var units = await _db.Units
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var selectedUnitExists = true;
        if (unitId.HasValue)
        {
            selectedUnitExists = await _db.Units
                .AsNoTracking()
                .AnyAsync(u => u.Id == unitId.Value && u.IsActive);

            if (!selectedUnitExists)
            {
                ModelState.AddModelError(nameof(unitId), "واحد انتخاب‌شده فعال نیست یا وجود ندارد.");
            }
        }

        IQueryable<AttendanceRecord> q = _db.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.Person)
            .Include(r => r.Unit)
            .Include(r => r.RegisteredByUser);

        if (startDate.HasValue)
        {
            q = q.Where(r => r.EntryAtUtc >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            q = q.Where(r => r.EntryAtUtc < endDate.Value.AddDays(1));
        }

        if (unitId.HasValue && selectedUnitExists)
        {
            q = q.Where(r => r.UnitId == unitId.Value);
        }

        if (status == 0)
        {
            q = q.Where(r => r.ExitAtUtc == null);
        }
        else if (status == 1)
        {
            q = q.Where(r => r.ExitAtUtc != null);
        }

        var filtersAreValid = ModelState.IsValid;
        if (!filtersAreValid)
        {
            q = _db.AttendanceRecords.AsNoTracking().Where(r => false);
        }

        var total = await q.CountAsync();
        var totalPages = total == 0 ? 1 : (total + pageSize - 1) / pageSize;
        if (page > totalPages)
        {
            page = totalPages;
        }

        var items = await q
            .OrderByDescending(r => r.EntryAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new ReportIndexViewModel
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            Start = startDate,
            End = endDate,
            UnitId = unitId,
            Status = status,
            Units = units
        };

        ViewBag.TotalPages = totalPages;
        return View(vm);
    }
}
