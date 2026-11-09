using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models;
using TourismMvc.Services;

namespace TourismMvc.Controllers;

public class HomeController(IHomeService homeService) : Controller
{
    public async Task<IActionResult> Index(string? search, int? destinationId, string? budget)
    {
        return View(await homeService.GetHomeAsync(search, destinationId, budget));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
