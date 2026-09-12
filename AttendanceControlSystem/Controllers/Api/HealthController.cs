using AttendanceControlSystem.Data;
using AttendanceControlSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AttendanceDbContext _db;
    private readonly BackupService _backup;
    public HealthController(AttendanceDbContext db, BackupService backup) { _db = db; _backup = backup; }

    [HttpGet("live")]
    [AllowAnonymous]
    public IActionResult Live() => Ok(new { status = "ok" });

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get()
    {
        var cs = _db.Database.GetConnectionString() ?? "Data Source=App_Data/attendance.db";
        var dbFile = Path.GetFullPath(cs.Split('=', 2)[1].Trim());
        var lastBackup = _backup.List().FirstOrDefault();
        return Ok(new
        {
            database = "ok",
            counts = new
            {
                people = await _db.People.CountAsync(),
                records = await _db.AttendanceRecords.CountAsync(),
                units = await _db.Units.CountAsync(),
                users = await _db.Users.CountAsync()
            },
            dbSizeBytes = System.IO.File.Exists(dbFile) ? new FileInfo(dbFile).Length : 0,
            lastBackupUtc = lastBackup.Created,
            version = "1.0.0"
        });
    }
}
