using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace AttendanceSystem.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private const int MaximumPageSize = 100;
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;

    public AttendanceController(AttendanceDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, int? personId = null, string? nationalId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var normalizedNationalId = NationalIdValidator.Normalize(nationalId);
        var hasNationalId = !string.IsNullOrWhiteSpace(nationalId);
        if (hasNationalId && !NationalIdValidator.IsValid(normalizedNationalId))
        {
            ModelState.AddModelError(nameof(nationalId), "کد ملی واردشده معتبر نیست.");
        }

        IQueryable<AttendanceRecord> q = _db.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.Person)
            .Include(r => r.Unit);

        if (personId.HasValue) q = q.Where(r => r.PersonId == personId.Value);
        if (hasNationalId && NationalIdValidator.IsValid(normalizedNationalId))
        {
            q = q.Where(r => r.Person!.NationalId == normalizedNationalId);
        }

        var total = await q.CountAsync();
        var totalPages = GetTotalPages(total, pageSize);
        if (page > totalPages)
        {
            page = totalPages;
        }

        var items = await q
            .OrderByDescending(r => r.EntryAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new AttendanceIndexViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
            PersonId = personId,
            NationalId = hasNationalId ? normalizedNationalId : null
        };

        ViewBag.TotalPages = totalPages;
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadUnitsAsync();
        return View(new AttendanceCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AttendanceCreateViewModel vm)
    {
        vm.NationalId = NationalIdValidator.Normalize(vm.NationalId);
        ModelState.Remove(nameof(vm.NationalId));

        var description = NormalizeOptionalText(vm.Description);
        var note = NormalizeOptionalText(vm.Note);
        vm.Description = description;
        vm.Note = note;

        if (!NationalIdValidator.IsValid(vm.NationalId))
        {
            ModelState.AddModelError(nameof(vm.NationalId), "کد ملی معتبر نیست.");
        }

        if (!ModelState.IsValid)
        {
            await LoadUnitsAsync();
            return View(vm);
        }

        var person = await _db.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.NationalId == vm.NationalId && p.IsActive);

        if (person is null)
        {
            ModelState.AddModelError(nameof(vm.NationalId), "فرد فعال با این کد ملی وجود ندارد.");
            await LoadUnitsAsync();
            return View(vm);
        }

        var unit = vm.UnitId.HasValue
            ? await _db.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == vm.UnitId.Value && u.IsActive)
            : null;

        if (unit is null)
        {
            ModelState.AddModelError(nameof(vm.UnitId), "واحد انتخاب‌شده فعال نیست یا وجود ندارد.");
        }
        else if (person.UnitId != unit.Id)
        {
            ModelState.AddModelError(nameof(vm.UnitId), "واحد انتخاب‌شده با واحد فرد مطابقت ندارد.");
        }

        var registeredByUserId = GetCurrentUserId();
        if (!registeredByUserId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "شناسه کاربر فعلی معتبر نیست.");
        }
        else
        {
            var currentUserIsValid = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == registeredByUserId.Value && u.IsActive && !u.IsLocked);

            if (!currentUserIsValid)
            {
                ModelState.AddModelError(string.Empty, "کاربر فعلی غیرفعال یا قفل‌شده است.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadUnitsAsync();
            return View(vm);
        }

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var openRecordExists = await _db.AttendanceRecords
                .AsNoTracking()
                .AnyAsync(r => r.PersonId == person.Id && r.ExitAtUtc == null);

            if (openRecordExists)
            {
                ModelState.AddModelError(string.Empty, "این فرد هم‌اکنون رکورد ورود بازی دارد.");
                await LoadUnitsAsync();
                return View(vm);
            }

            var currentUserId = registeredByUserId!.Value;
            var record = new AttendanceRecord
            {
                PersonId = person.Id,
                UnitId = unit!.Id,
                EntryAtUtc = DateTime.UtcNow,
                EntryDescription = description,
                ExitDescription = string.Empty,
                Note = note,
                RegisteredByUserId = currentUserId
            };

            _db.AttendanceRecords.Add(record);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Entry", entityName: nameof(AttendanceRecord), details: person.NationalId);
            await transaction.CommitAsync();

            return RedirectToAction("Index");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(string.Empty, "این فرد هم‌اکنون رکورد ورود بازی دارد.");
            await LoadUnitsAsync();
            return View(vm);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await _db.AttendanceRecords
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record is null) return NotFound();
        if (record.ExitAtUtc is not null) return NotFound();

        return View(ToEditViewModel(record));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AttendanceEditViewModel vm, DateTime? exitAtUtc)
    {
        var record = await _db.AttendanceRecords
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == vm.Id);

        if (record is null) return NotFound();

        var editVm = ToEditViewModel(record);
        editVm.ExitDescription = vm.ExitDescription;
        editVm.Note = vm.Note;

        if (record.ExitAtUtc is not null)
        {
            ModelState.AddModelError(string.Empty, "این رکورد قبلاً خروج دارد.");
            return View(editVm);
        }

        var exitTime = exitAtUtc.HasValue
            ? DateTime.SpecifyKind(exitAtUtc.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;
        var now = DateTime.UtcNow;

        if (exitTime <= record.EntryAtUtc)
        {
            ModelState.AddModelError(nameof(exitAtUtc), "زمان خروج باید بعد از زمان ورود باشد.");
        }
        else if (exitTime > now)
        {
            ModelState.AddModelError(nameof(exitAtUtc), "زمان خروج نمی‌تواند در آینده باشد.");
        }

        if (!ModelState.IsValid)
        {
            return View(editVm);
        }

        var exitDescription = NormalizeOptionalText(vm.ExitDescription);
        var note = NormalizeOptionalText(vm.Note);
        editVm.ExitDescription = exitDescription;
        editVm.Note = note;

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var updatedRows = await _db.AttendanceRecords
            .Where(r => r.Id == record.Id && r.ExitAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.ExitAtUtc, exitTime)
                .SetProperty(r => r.ExitDescription, exitDescription)
                .SetProperty(r => r.Note, note));

        if (updatedRows != 1)
        {
            ModelState.AddModelError(string.Empty, "این رکورد دیگر باز نیست و خروج برای آن ثبت نشد.");
            return View(editVm);
        }

        await _audit.LogAsync("Exit", entityName: nameof(AttendanceRecord), details: record.Person.NationalId);
        await transaction.CommitAsync();

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var record = await _db.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.Person)
            .Include(r => r.Unit)
            .Include(r => r.RegisteredByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record is null) return NotFound();
        return View(record);
    }

    private async Task LoadUnitsAsync()
    {
        ViewBag.Units = await _db.Units
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Name
            })
            .ToListAsync();
    }

    private AttendanceEditViewModel ToEditViewModel(AttendanceRecord record)
    {
        return new AttendanceEditViewModel
        {
            Id = record.Id,
            PersonName = $"{record.Person.FirstName} {record.Person.LastName}",
            NationalId = record.Person.NationalId,
            EntryAtUtc = record.EntryAtUtc,
            ExitDescription = string.IsNullOrWhiteSpace(record.ExitDescription)
                ? string.Empty
                : record.ExitDescription,
            Note = string.IsNullOrWhiteSpace(record.Note) ? string.Empty : record.Note
        };
    }

    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("UserId");

        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static string NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static int GetTotalPages(int total, int pageSize)
    {
        return total == 0 ? 1 : (total + pageSize - 1) / pageSize;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqliteException
                && sqliteException.SqliteErrorCode is 19 or 2067)
            {
                return true;
            }
        }

        return false;
    }
}
