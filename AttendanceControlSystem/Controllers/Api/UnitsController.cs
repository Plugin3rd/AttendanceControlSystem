using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/units")]
[Authorize]
public class UnitsController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    public UnitsController(AttendanceDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _db.Units.OrderBy(u => u.Name).AsNoTracking().ToListAsync());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] UnitDto dto)
    {
        if (await _db.Units.AnyAsync(u => u.Name == dto.Name))
            return Conflict(new { error = "واحد تکراری است." });
        var unit = new Unit { Name = dto.Name, IsActive = dto.IsActive };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync();
        return Ok(unit);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UnitDto dto)
    {
        var unit = await _db.Units.FindAsync(id);
        if (unit is null) return NotFound();
        if (await _db.Units.AnyAsync(u => u.Name == dto.Name && u.Id != id))
            return Conflict(new { error = "واحد تکراری است." });
        unit.Name = dto.Name; unit.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return Ok(unit);
    }

    [HttpPatch("{id}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Toggle(int id)
    {
        var unit = await _db.Units.FindAsync(id);
        if (unit is null) return NotFound();
        unit.IsActive = !unit.IsActive;
        await _db.SaveChangesAsync();
        return Ok(unit);
    }
}
