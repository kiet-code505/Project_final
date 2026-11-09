using Microsoft.AspNetCore.Mvc;
using TourismMvc.Services;

namespace TourismMvc.Controllers;

public class PublicTourDetailController(IPublicTourDetailService detailService) : Controller
{
    [HttpGet("/tour/{tourCode}")]
    public async Task<IActionResult> Details(string tourCode, int? departureId)
    {
        var vm = await detailService.GetAsync(tourCode, departureId);
        if (vm != null) return View("~/Views/PublicTours/Details.cshtml", vm);
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("~/Views/PublicTours/NotFound.cshtml");
    }
}
