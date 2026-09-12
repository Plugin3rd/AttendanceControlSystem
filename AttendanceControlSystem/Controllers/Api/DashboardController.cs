using AttendanceControlSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    public DashboardController(AttendanceDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var today = DateTime.UtcNow.Date;
        return Ok(new
        {
            presentCount = await _db.AttendanceRecords.CountAsync(r => r.ExitAtUtc == null),
            todayEntries = await _db.AttendanceRecords.CountAsync(r => r.EntryAtUtc >= today),
            todayExits = await _db.AttendanceRecords.CountAsync(r => r.ExitAtUtc >= today),
            peopleCount = await _db.People.CountAsync(p => p.IsActive),
            unitsCount = await _db.Units.CountAsync(u => u.IsActive),
            recent = await _db.AttendanceRecords.Include(r => r.Person).Include(r => r.Unit).AsNoTracking()
                .OrderByDescending(r => r.EntryAtUtc).Take(15).ToListAsync()
        });
    }
}
