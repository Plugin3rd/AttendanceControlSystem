using AttendanceControlSystem_vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = HttpContext.TraceIdentifier;
        ViewData["RequestId"] = requestId;
        ViewData["ErrorMessage"] = "خطای داخلی سامانه.";
        _logger.LogError("Request {RequestId} failed.", requestId);

        return View(new ErrorViewModel
        {
            Message = "متأسفانه خطایی رخ داد. لطفاً دوباره تلاش کنید."
        });
    }
}
