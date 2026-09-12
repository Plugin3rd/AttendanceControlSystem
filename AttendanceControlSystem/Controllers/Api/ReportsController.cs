using System.Text;
using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    public ReportsController(AttendanceDbContext db) { _db = db; }

    private IQueryable<AttendanceRecord> Query(DateTime? start, DateTime? end, int? personId,
        string? nationalId, int? unitId, int? status)
    {
        var q = _db.AttendanceRecords.Include(r => r.Person).Include(r => r.Unit).AsNoTracking();
        if (start.HasValue) q = q.Where(r => r.EntryAtUtc >= start);
        if (end.HasValue) q = q.Where(r => r.EntryAtUtc <= end);
        if (personId.HasValue) q = q.Where(r => r.PersonId == personId);
        if (!string.IsNullOrWhiteSpace(nationalId)) q = q.Where(r => r.Person!.NationalId == nationalId);
        if (unitId.HasValue) q = q.Where(r => r.UnitId == unitId);
        if (status == 1) q = q.Where(r => r.ExitAtUtc == null);
        else if (status == 2) q = q.Where(r => r.ExitAtUtc != null);
        return q;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime? start, [FromQuery] DateTime? end,
        [FromQuery] int? personId, [FromQuery] string? nationalId, [FromQuery] int? unitId,
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 1000) pageSize = 20;
        var q = Query(start, end, personId, nationalId, unitId, status);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(r => r.EntryAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<AttendanceRecord>(items, page, pageSize, total));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime? start, [FromQuery] DateTime? end,
        [FromQuery] int? personId, [FromQuery] string? nationalId, [FromQuery] int? unitId, [FromQuery] int? status,
        [FromQuery] bool open = false)
    {
        var q = Query(start, end, personId, nationalId, unitId, status);
        if (open) q = q.Where(r => r.ExitAtUtc == null);
        var rows = await q.OrderByDescending(r => r.EntryAtUtc).Take(50000).ToListAsync();
        var sb = new StringBuilder("Id,FirstName,LastName,NationalId,Unit,EntryUtc,ExitUtc,Note\n");
        foreach (var r in rows)
        {
            sb.Append(r.Id).Append(',').Append(r.Person?.FirstName).Append(',').Append(r.Person?.LastName).Append(',')
              .Append(r.Person?.NationalId).Append(',').Append(r.Unit?.Name).Append(',')
              .Append(r.EntryAtUtc.ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
              .Append(r.ExitAtUtc?.ToString("yyyy-MM-dd HH:mm:ss")).Append(",\"").Append(r.Note).Append("\"\n");
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
            "text/csv; charset=utf-8", "report.csv");
    }
}
