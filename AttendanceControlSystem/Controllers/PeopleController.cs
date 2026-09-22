using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AttendanceSystem.Controllers;

[Authorize]
public class PeopleController : Controller
{
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;

    public PeopleController(AttendanceDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? search = null)
    {
        page = Math.Max(MinimumPageSize, page);
        pageSize = Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize);
        var searchValue = search?.Trim() ?? string.Empty;
        var normalizedSearch = NormalizeNationalId(searchValue);
        IQueryable<Person> query = _db.People.AsNoTracking().Include(p => p.Unit);

        if (!string.IsNullOrWhiteSpace(searchValue))
        {
            query = !string.IsNullOrEmpty(normalizedSearch)
                ? query.Where(p =>
                    p.FirstName.Contains(searchValue) ||
                    p.LastName.Contains(searchValue) ||
                    p.Organization.Contains(searchValue) ||
                    p.Phone.Contains(searchValue) ||
                    p.NationalId.Contains(normalizedSearch))
                : query.Where(p =>
                    p.FirstName.Contains(searchValue) ||
                    p.LastName.Contains(searchValue) ||
                    p.Organization.Contains(searchValue) ||
                    p.Phone.Contains(searchValue));
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
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TotalPages = totalPages;
        return View(new PeopleIndexViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
            Search = searchValue
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUnitsAsync();
        return View(new PersonCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PersonCreateViewModel vm)
    {
        vm ??= new PersonCreateViewModel();
        TrimPersonCreateViewModel(vm);
        if (string.IsNullOrWhiteSpace(vm.FirstName))
        {
            ModelState.AddModelError(nameof(vm.FirstName), "نام الزامی است.");
        }
        if (string.IsNullOrWhiteSpace(vm.LastName))
        {
            ModelState.AddModelError(nameof(vm.LastName), "نام خانوادگی الزامی است.");
        }
        ModelState.Remove(nameof(vm.NationalId));

        if (!TryNormalizeNationalId(vm.NationalId, out var normalizedNationalId, out var nationalIdError))
        {
            ModelState.AddModelError(nameof(vm.NationalId), nationalIdError);
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        vm.NationalId = normalizedNationalId;
        if (!ModelState.IsValid)
        {
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        if (!await ValidateUnitAsync(vm.UnitId))
        {
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        if (await _db.People.AnyAsync(p => p.NationalId == normalizedNationalId))
        {
            ModelState.AddModelError(nameof(vm.NationalId), "کد ملی تکراری است.");
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        var person = new Person
        {
            NationalId = normalizedNationalId,
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            Phone = vm.Phone ?? string.Empty,
            Organization = vm.Organization ?? string.Empty,
            UnitId = vm.UnitId,
            IsActive = vm.IsActive
        };

        _db.People.Add(person);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _db.People.AnyAsync(p => p.NationalId == normalizedNationalId))
            {
                ModelState.AddModelError(nameof(vm.NationalId), "کد ملی تکراری است.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "ذخیره اطلاعات انجام نشد.");
            }

            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        await _audit.LogAsync("PersonCreate", details: person.NationalId);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var person = await _db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (person is null)
        {
            return NotFound();
        }

        var vm = new PersonEditViewModel
        {
            Id = person.Id,
            NationalId = person.NationalId,
            FirstName = person.FirstName,
            LastName = person.LastName,
            Phone = person.Phone,
            Organization = person.Organization,
            UnitId = person.UnitId,
            IsActive = person.IsActive
        };

        await PopulateUnitsAsync(person.UnitId, true);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PersonEditViewModel vm)
    {
        vm ??= new PersonEditViewModel();
        if (vm.Id <= 0)
        {
            ModelState.AddModelError(nameof(vm.Id), "شناسه فرد معتبر نیست.");
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == vm.Id);
        if (person is null)
        {
            return NotFound();
        }

        TrimPersonEditViewModel(vm);
        if (string.IsNullOrWhiteSpace(vm.FirstName))
        {
            ModelState.AddModelError(nameof(vm.FirstName), "نام الزامی است.");
        }
        if (string.IsNullOrWhiteSpace(vm.LastName))
        {
            ModelState.AddModelError(nameof(vm.LastName), "نام خانوادگی الزامی است.");
        }
        ModelState.Remove(nameof(vm.NationalId));

        if (!TryNormalizeNationalId(vm.NationalId, out var normalizedNationalId, out var nationalIdError))
        {
            ModelState.AddModelError(nameof(vm.NationalId), nationalIdError);
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        vm.NationalId = normalizedNationalId;
        if (!ModelState.IsValid)
        {
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        if (!await ValidateUnitAsync(vm.UnitId))
        {
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        if (await _db.People.AnyAsync(p => p.NationalId == normalizedNationalId && p.Id != person.Id))
        {
            ModelState.AddModelError(nameof(vm.NationalId), "کد ملی تکراری است.");
            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        person.NationalId = normalizedNationalId;
        person.FirstName = vm.FirstName;
        person.LastName = vm.LastName;
        person.Phone = vm.Phone ?? string.Empty;
        person.Organization = vm.Organization ?? string.Empty;
        person.UnitId = vm.UnitId;
        person.IsActive = vm.IsActive;
        person.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _db.People.AnyAsync(p => p.NationalId == normalizedNationalId && p.Id != person.Id))
            {
                ModelState.AddModelError(nameof(vm.NationalId), "کد ملی تکراری است.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "ذخیره اطلاعات انجام نشد.");
            }

            await PopulateUnitsAsync(vm.UnitId, true);
            return View(vm);
        }

        await _audit.LogAsync("PersonEdit", details: person.NationalId);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var person = await _db.People
            .AsNoTracking()
            .Include(p => p.Unit)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null)
        {
            return NotFound();
        }

        return View(person);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id);
        if (person is null)
        {
            return NotFound();
        }

        person.IsActive = false;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("PersonDelete", details: person.NationalId);
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUnitsAsync(int? selectedUnitId = null, bool includeSelectedUnit = false)
    {
        var units = await _db.Units
            .AsNoTracking()
            .Where(u => u.IsActive || (includeSelectedUnit && selectedUnitId.HasValue && u.Id == selectedUnitId.Value))
            .OrderBy(u => u.Name)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Text = u.IsActive ? u.Name : $"{u.Name} (غیرفعال)"
            })
            .ToListAsync();

        units.Insert(0, new SelectListItem { Value = string.Empty, Text = "بدون واحد" });
        ViewBag.Units = units;
    }

    private async Task<bool> ValidateUnitAsync(int? unitId)
    {
        if (!unitId.HasValue)
        {
            return true;
        }

        var unit = await _db.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unitId.Value);

        if (unit is null)
        {
            ModelState.AddModelError(nameof(PersonCreateViewModel.UnitId), "واحد انتخاب‌شده وجود ندارد.");
            return false;
        }

        if (!unit.IsActive)
        {
            ModelState.AddModelError(nameof(PersonCreateViewModel.UnitId), "واحد انتخاب‌شده فعال نیست.");
            return false;
        }

        return true;
    }

    private static int CalculateTotalPages(int total, int pageSize)
    {
        return total == 0 ? 0 : (total + pageSize - 1) / pageSize;
    }

    private static bool TryNormalizeNationalId(string? value, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "کد ملی الزامی است.";
            return false;
        }

        var trimmedValue = value.Trim();
        foreach (var character in trimmedValue)
        {
            if (!IsNationalIdDigit(character) && !char.IsWhiteSpace(character) && character != '-')
            {
                error = "کد ملی فقط می‌تواند شامل رقم، فاصله و خط تیره باشد.";
                return false;
            }
        }

        normalized = NormalizeNationalId(trimmedValue);
        if (normalized.Length != 10 || !NationalIdValidator.IsValid(normalized))
        {
            error = "کد ملی نامعتبر است.";
            return false;
        }

        return true;
    }

    private static string NormalizeNationalId(string value)
    {
        return new string(value.Where(IsNationalIdDigit).Select(NormalizeNationalIdDigit).ToArray());
    }

    private static char NormalizeNationalIdDigit(char character)
    {
        if (character >= '0' && character <= '9')
        {
            return character;
        }

        if (character >= '\u06F0' && character <= '\u06F9')
        {
            return (char)('0' + (character - '\u06F0'));
        }

        return (char)('0' + (character - '\u0660'));
    }

    private static bool IsNationalIdDigit(char character)
    {
        return (character >= '0' && character <= '9') ||
               (character >= '\u06F0' && character <= '\u06F9') ||
               (character >= '\u0660' && character <= '\u0669');
    }

    private static void TrimPersonCreateViewModel(PersonCreateViewModel vm)
    {
        vm.FirstName = vm.FirstName?.Trim() ?? string.Empty;
        vm.LastName = vm.LastName?.Trim() ?? string.Empty;
        vm.Phone = vm.Phone?.Trim() ?? string.Empty;
        vm.Organization = vm.Organization?.Trim() ?? string.Empty;
    }

    private static void TrimPersonEditViewModel(PersonEditViewModel vm)
    {
        vm.FirstName = vm.FirstName?.Trim() ?? string.Empty;
        vm.LastName = vm.LastName?.Trim() ?? string.Empty;
        vm.Phone = vm.Phone?.Trim() ?? string.Empty;
        vm.Organization = vm.Organization?.Trim() ?? string.Empty;
    }
}
