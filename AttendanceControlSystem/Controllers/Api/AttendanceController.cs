using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;
    public AttendanceController(AttendanceDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] DateTime? start, [FromQuery] DateTime? end,
        [FromQuery] int? personId, [FromQuery] string? nationalId, [FromQuery] int? unitId,
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 20;
        var q = _db.AttendanceRecords.Include(r => r.Person).Include(r => r.Unit).AsNoTracking();
        if (start.HasValue) q = q.Where(r => r.EntryAtUtc >= start);
        if (end.HasValue) q = q.Where(r => r.EntryAtUtc <= end);
        if (personId.HasValue) q = q.Where(r => r.PersonId == personId);
        if (!string.IsNullOrWhiteSpace(nationalId)) q = q.Where(r => r.Person!.NationalId == nationalId);
        if (unitId.HasValue) q = q.Where(r => r.UnitId == unitId);
        if (status == 1) q = q.Where(r => r.ExitAtUtc == null);
        else if (status == 2) q = q.Where(r => r.ExitAtUtc != null);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(r => r.EntryAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<AttendanceRecord>(items, page, pageSize, total));
    }

    [HttpGet("present")]
    public async Task<IActionResult> Present()
    {
        var items = await _db.AttendanceRecords.Include(r => r.Person).Include(r => r.Unit)
            .Where(r => r.ExitAtUtc == null).AsNoTracking()
            .OrderBy(r => r.EntryAtUtc).ToListAsync();
        return Ok(items);
    }

    [HttpPost("entry")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Entry([FromBody] AttendanceEntryDto dto)
    {
        var nid = NationalIdValidator.Normalize(dto.NationalId);
        var person = await _db.People.FirstOrDefaultAsync(p => p.NationalId == nid && p.IsActive);
        if (person is null) return NotFound(new { error = "فرد فعال با این کد ملی یافت نشد." });
        if (dto.UnitId.HasValue && !await _db.Units.AnyAsync(u => u.Id == dto.UnitId && u.IsActive))
            return BadRequest(new { error = "واحد نامعتبر یا غیرفعال است." });
        if (await _db.AttendanceRecords.AnyAsync(r => r.PersonId == person.Id && r.ExitAtUtc == null))
            return Conflict(new { error = "این فرد رکورد ورود بازی دارد." });

        using var tx = await _db.Database.BeginTransactionAsync();
        var rec = new AttendanceRecord { PersonId = person.Id, UnitId = dto.UnitId ?? person.UnitId,
            EntryAtUtc = DateTime.UtcNow, Note = dto.Note };
        _db.AttendanceRecords.Add(rec);
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "Entry", Details = nid });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(rec);
    }

    [HttpPost("exit")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Exit([FromBody] AttendanceExitDto dto)
    {
        var nid = NationalIdValidator.Normalize(dto.NationalId);
        var person = await _db.People.FirstOrDefaultAsync(p => p.NationalId == nid);
        if (person is null) return NotFound(new { error = "فرد با این کد ملی یافت نشد." });
        var open = await _db.AttendanceRecords.FirstOrDefaultAsync(r => r.PersonId == person.Id && r.ExitAtUtc == null);
        if (open is null) return Conflict(new { error = "رکورد بازی برای این فرد وجود ندارد." });
        open.ExitAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Note)) open.Note = dto.Note;
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "Exit", Details = nid });
        await _db.SaveChangesAsync();
        return Ok(open);
    }

    [HttpPost("quick-exit/{recordId}")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> QuickExit(int recordId)
    {
        var open = await _db.AttendanceRecords.Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.ExitAtUtc == null);
        if (open is null) return NotFound(new { error = "رکورد باز یافت نشد." });
        open.ExitAtUtc = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "QuickExit", Details = open.Person?.NationalId });
        await _db.SaveChangesAsync();
        return Ok(open);
    }
}
