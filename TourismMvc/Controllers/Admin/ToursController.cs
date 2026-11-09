using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

[Route("admin/tours")]
public class ToursController(ITourService tourService) : AdminControllerBase
{
    private const string FormView = "~/Views/Admin/Tours/Form.cshtml";

    // GET /admin/tours
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int? startLocationId, int? endLocationId, bool? status,
        string sortBy = "updatedAt", string sortDir = "DESC", int page = 1, int pageSize = 10)
    {
        var vm = await tourService.GetPagedToursAsync(search, startLocationId, endLocationId, status,
            sortBy, sortDir, page, pageSize);
        return View("~/Views/Admin/Tours/Index.cshtml", vm);
    }

    // GET /admin/tours/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var vm = await tourService.GetTourDetailAsync(id);
        if (vm == null)
        {
            TempData["Error"] = $"Không tìm thấy tour với ID: {id}";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/Admin/Tours/Details.cshtml", vm);
    }

    // GET /admin/tours/check-code?tourCode=...&excludeId=...  (AJAX)
    [HttpGet("check-code")]
    public async Task<IActionResult> CheckCode(string tourCode, int? excludeId) =>
        Json(new { exists = await tourService.CheckTourCodeExistsAsync(tourCode, excludeId) });

    // GET /admin/tours/create
    [HttpGet("create")]
    public async Task<IActionResult> Create() => View(FormView, await tourService.PrepareCreateViewModelAsync());

    // POST /admin/tours/create
    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TourFormViewModel vm)
    {
        if (await tourService.CreateTourAsync(vm, ModelState))
        {
            TempData["Success"] = $"Đã tạo tour \"{vm.TourName}\"";
            return RedirectToAction(nameof(Index));
        }
        return View(FormView, vm);
    }

    // GET /admin/tours/{id}/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await tourService.GetForEditAsync(id);
        if (vm == null)
        {
            TempData["Error"] = $"Không tìm thấy tour với ID: {id}";
            return RedirectToAction(nameof(Index));
        }
        return View(FormView, vm);
    }

    // POST /admin/tours/{id}/edit
    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TourFormViewModel vm)
    {
        if (await tourService.UpdateTourAsync(id, vm, ModelState))
        {
            TempData["Success"] = $"Đã cập nhật tour \"{vm.TourName}\"";
            return RedirectToAction(nameof(Index));
        }
        return View(FormView, vm);
    }

    // POST /admin/tours/{id}/toggle-status
    [HttpPost("{id:int}/toggle-status"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string? returnUrl)
    {
        var (ok, newStatus) = await tourService.ToggleStatusAsync(id);
        if (ok) TempData["Success"] = newStatus ? "Đã kích hoạt tour." : "Đã tạm dừng tour.";
        else TempData["Error"] = $"Không tìm thấy tour với ID: {id}";
        return RedirectBack(returnUrl);
    }

    // POST /admin/tours/{id}/delete
    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var (ok, error) = await tourService.DeleteTourAsync(id);
        if (ok) TempData["Success"] = "Đã xóa tour thành công.";
        else TempData["Error"] = error ?? "Không thể xóa tour.";
        return RedirectBack(returnUrl);
    }

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
