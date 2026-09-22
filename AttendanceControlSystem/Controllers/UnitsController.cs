using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Controllers;

[Authorize]
public class UnitsController : Controller
{
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;

    public UnitsController(AttendanceDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? search = null)
    {
        page = Math.Max(MinimumPageSize, page);
        pageSize = Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize);
        var searchValue = search?.Trim() ?? string.Empty;
        var query = _db.Units.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchValue))
        {
            query = query.Where(u =>
                u.Name.Contains(searchValue) ||
                u.Description.Contains(searchValue));
        }

        var total = await query.CountAsync();
        var totalPages = CalculateTotalPages(total, pageSize);
        if (totalPages == 0)
        {
            page = 1;
        }
        else if (page > totalPages)
        {
            page = totalPages;
        }

        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TotalPages = totalPages;
        ViewBag.Page = page;
        ViewBag.Search = searchValue;
        ViewBag.PageSize = pageSize;
        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new UnitCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitCreateViewModel vm)
    {
        vm ??= new UnitCreateViewModel();
        vm.Name = vm.Name?.Trim() ?? string.Empty;
        vm.Description = vm.Description?.Trim();
        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            ModelState.AddModelError(nameof(vm.Name), "نام واحد الزامی است.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var unit = new Unit
        {
            Name = vm.Name,
            Description = vm.Description ?? string.Empty,
            IsActive = vm.IsActive
        };

        _db.Units.Add(unit);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "ذخیره اطلاعات انجام نشد.");
            return View(vm);
        }

        await _audit.LogAsync("UnitCreate", details: unit.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var unit = await _db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (unit is null)
        {
            return NotFound();
        }

        return View(new UnitEditViewModel
        {
            Id = unit.Id,
            Name = unit.Name,
            Description = unit.Description,
            IsActive = unit.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UnitEditViewModel vm)
    {
        vm ??= new UnitEditViewModel();
        if (vm.Id <= 0)
        {
            ModelState.AddModelError(nameof(vm.Id), "شناسه واحد معتبر نیست.");
            return View(vm);
        }

        var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == vm.Id);
        if (unit is null)
        {
            return NotFound();
        }

        vm.Name = vm.Name?.Trim() ?? string.Empty;
        vm.Description = vm.Description?.Trim();
        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            ModelState.AddModelError(nameof(vm.Name), "نام واحد الزامی است.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        unit.Name = vm.Name;
        unit.Description = vm.Description ?? string.Empty;
        unit.IsActive = vm.IsActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UnitEdit", details: unit.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var unit = await _db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (unit is null)
        {
            return NotFound();
        }

        return View(unit);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == id);
        if (unit is null)
        {
            return NotFound();
        }

        unit.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UnitDelete", details: unit.Name);
        return RedirectToAction(nameof(Index));
    }

    private static int CalculateTotalPages(int total, int pageSize)
    {
        return total == 0 ? 0 : (total + pageSize - 1) / pageSize;
    }
}
