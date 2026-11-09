using Microsoft.AspNetCore.Mvc;

namespace TourismMvc.Controllers.Admin;

public class AdminHomeController : AdminControllerBase
{
    [HttpGet("/admin"), HttpGet("/admin/dashboard")]
    public IActionResult Index() => Redirect("/admin/tours");
}
