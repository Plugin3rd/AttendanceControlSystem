using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;
using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Controllers;

[Authorize(Roles = "Admin")]
public class BackupController : Controller
{
    private readonly BackupService _backup;
    private readonly AuditService _audit;

    public BackupController(BackupService backup, AuditService audit)
    {
        _backup = backup;
        _audit = audit;
    }

    public IActionResult Index()
    {
        var files = _backup.List();
        return View(files);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var (name, size, error) = await _backup.CreateBackupAsync(cancellationToken);
        if (error is not null)
        {
            ViewBag.Error = error;
            return View("Index", _backup.List());
        }
        await _audit.LogAsync("BackupCreate", details: name, cancellationToken: cancellationToken);
        return RedirectToAction("Index");
    }

    public IActionResult Download(string name)
    {
        var (content, fileName) = _backup.Download(name);
        if (content is null) return NotFound();
        return File(content, "application/octet-stream", fileName);
    }
}