using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers;

[Route("tours")]
public class PublicToursController(IPublicTourSearchService searchService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] PublicTourSearchFilter filter)
    {
        if (!ModelState.IsValid)
            ModelState.AddModelError("", "Bộ lọc chưa hợp lệ. Vui lòng kiểm tra lại điểm đến và số trang.");
        return View("~/Views/PublicTours/Index.cshtml", await searchService.SearchAsync(filter));
    }
}
