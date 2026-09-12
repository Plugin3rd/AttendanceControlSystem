using System.Text;
using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/people")]
[Authorize]
public class PeopleController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;
    public PeopleController(AttendanceDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;
        var query = _db.People.Include(p => p.Unit).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.FirstName.Contains(q) || p.LastName.Contains(q) || p.NationalId.Contains(q));
        var total = await query.CountAsync();
        var items = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<Person>(items, page, pageSize, total));
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;
        var query = _db.AttendanceRecords.Include(r => r.Unit).Where(r => r.PersonId == id).AsNoTracking();
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.EntryAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<AttendanceRecord>(items, page, pageSize, total));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Create([FromBody] PersonDto dto)
    {
        var nid = NationalIdValidator.Normalize(dto.NationalId);
        if (!NationalIdValidator.IsValid(nid))
            return BadRequest(new { error = "کد ملیکد ملی نامعتبر است." });
        if (await _db.People.AnyAsync(p => p.NationalId == nid))
            return Conflict(new { error = "کد ملی تکراری است." });
        var person = new Person { FirstName = dto.FirstName, LastName = dto.LastName, NationalId = nid,
            Phone = dto.Phone, UnitId = dto.UnitId, IsActive = dto.IsActive };
        _db.People.Add(person);
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "PersonCreate", Details = nid });
        await _db.SaveChangesAsync();
        return Ok(person);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Update(int id, [FromBody] PersonDto dto)
    {
        var person = await _db.People.FindAsync(id);
        if (person is null) return NotFound();
        var nid = NationalIdValidator.Normalize(dto.NationalId);
        if (!NationalIdValidator.IsValid(nid)) return BadRequest(new { error = "کد ملی نامعتبر است." });
        if (await _db.People.AnyAsync(p => p.NationalId == nid && p.Id != id))
            return Conflict(new { error = "کد ملی تکراری است." });
        person.FirstName = dto.FirstName; person.LastName = dto.LastName; person.NationalId = nid;
        person.Phone = dto.Phone; person.UnitId = dto.UnitId; person.IsActive = dto.IsActive;
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "PersonUpdate", Details = nid });
        await _db.SaveChangesAsync();
        return Ok(person);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var person = await _db.People.FindAsync(id);
        if (person is null) return NotFound();
        person.IsActive = false;
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "PersonDeactivate", Details = person.NationalId });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, deactivated = true });
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin,Operator")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "فایلی ارسال نشده است." });
        var errors = new List<ImportError>();
        int imported = 0, line = 1;
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? header = await reader.ReadLineAsync(); line++;
        if (header is null) return BadRequest(new { error = "فایل خالی است." });
        var cols = header.Trim().TrimStart((char)0xFEFF).Split(',');
        int fi = Idx(cols, "FirstName", "نام"), li = Idx(cols, "LastName", "نام خانوادگی"),
            ni = Idx(cols, "NationalId", "کد ملی"), pi = Idx(cols, "Phone", "تلفن"),
            ui = Idx(cols, "Unit", "واحد");
        if (fi < 0 || li < 0 || ni < 0) return BadRequest(new { error = "ستون‌های الزامی یافت نشد." });

        var unitMap = await _db.Units.ToDictionaryAsync(u => u.Name.Trim(), u => u.Id);
        while (await reader.ReadLineAsync() is { } row)
        {
            line++;
            if (string.IsNullOrWhiteSpace(row)) continue;
            if (imported + errors.Count >= 15000) { errors.Add(new ImportError(line, "حداکثر ۱۵۰۰۰ ردیف")); break; }
            var parts = row.Split(',');
            try
            {
                var nid = NationalIdValidator.Normalize(parts[ni]);
                if (!NationalIdValidator.IsValid(nid)) { errors.Add(new ImportError(line, "کد ملی نامعتبر")); continue; }
                if (await _db.People.AnyAsync(p => p.NationalId == nid)) { errors.Add(new ImportError(line, "کد ملی تکراری: " + nid)); continue; }
                int? unitId = null;
                if (ui >= 0 && ui < parts.Length && unitMap.TryGetValue(parts[ui].Trim(), out var uid)) unitId = uid;
                _db.People.Add(new Person { FirstName = parts[fi].Trim(), LastName = parts[li].Trim(),
                    NationalId = nid, Phone = pi >= 0 && pi < parts.Length ? parts[pi].Trim() : null, UnitId = unitId });
                imported++;
            }
            catch (Exception ex) { errors.Add(new ImportError(line, ex.Message)); }
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync(User.Identity?.Name, "PeopleImport", "imported=" + imported + ", errors=" + errors.Count);
        string? downloadId = null;
        if (errors.Count > 0)
        {
            downloadId = Guid.NewGuid().ToString("N") + ".csv";
            var path = Path.Combine(Path.GetTempPath(), "acs-" + downloadId);
            var sb = new StringBuilder("Line,Message\n");
            foreach (var e in errors)
                sb.Append(e.Line).Append(",").Append('"').Append(e.Message.Replace("\"", "'")).Append('"').Append('\n');
            await System.IO.File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
        }
        return Ok(new ImportErrorFile(imported, errors, downloadId));
    }

    [HttpGet("import-errors/{id}")]
    [Authorize(Roles = "Admin,Operator")]
    public IActionResult ImportErrors(string id)
    {
        if (!id.EndsWith(".csv") || id.Contains("..") || id.Contains('/') || id.Contains('\\'))
            return BadRequest();

        var path = Path.Combine(Path.GetTempPath(), "acs-" + id);

        if (!System.IO.File.Exists(path))
            return NotFound();

        return File(System.IO.File.ReadAllBytes(path), "text/csv; charset=utf-8", "import-errors.csv");
    }

    private static int Idx(string[] cols, params string[] names)
    {
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i].Trim().Trim('"');
            if (names.Contains(c, StringComparer.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
