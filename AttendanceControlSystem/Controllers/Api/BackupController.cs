using AttendanceControlSystem.Data;
using AttendanceControlSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceControlSystem.Controllers.Api;

[ApiController]
[Route("api/backup")]
[Authorize(Roles = "Admin")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backup;
    private readonly AttendanceDbContext _db;
    private readonly AuditService _audit;

    public BackupController(BackupService backup, AttendanceDbContext db, AuditService audit)
    { _backup = backup; _db = db; _audit = audit; }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var (name, size, error) = _backup.CreateBackup();
        if (error != null) return BadRequest(new { error });
        await _audit.LogAsync(User.Identity?.Name, "BackupCreate", name);
        return Ok(new { fileName = name, size });
    }

    [HttpGet]
    public IActionResult List() => Ok(_backup.List().Select(b => new { b.Name, b.Size, b.Created }));

    [HttpGet("download/{name}")]
    public IActionResult Download(string name)
    {
        var (content, fileName) = _backup.Download(name);
        if (content is null) return NotFound();
        return File(content, "application/octet-stream", fileName);
    }
}
