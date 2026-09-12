using Microsoft.AspNetCore.Mvc;

namespace AttendanceControlSystem.Controllers;

public class WebController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet]
    public IActionResult People()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Units()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Attendance()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Reports()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Users()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Backup()
    {
        return View();
    }
}
