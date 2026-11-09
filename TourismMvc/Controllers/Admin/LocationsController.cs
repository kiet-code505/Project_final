using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

/// <summary>
/// Controller Quản trị Địa điểm — Đã được refactor tinh gọn, 
/// toàn bộ nghiệp vụ được ủy quyền cho ILocationService.
/// </summary>
[Route("admin/locations")]
public class LocationsController(ILocationService locationService) : AdminControllerBase
{
    // GET /admin/locations
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, 
        Region? region, 
        string sortBy = "updatedAt",
        string sortDir = "DESC", 
        int page = 1, 
        int pageSize = 10)
    {
        var vm = await locationService.GetPagedLocationsAsync(search, region, sortBy, sortDir, page, pageSize);
        return View("~/Views/Admin/Locations/Index.cshtml", vm);
    }

    // GET /admin/locations/create
    [HttpGet("create")]
    public IActionResult Create() => View("~/Views/Admin/Locations/Form.cshtml", new LocationFormViewModel());

    // POST /admin/locations/create
    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LocationFormViewModel vm)
    {
        if (await locationService.CreateAsync(vm, ModelState))
        {
            TempData["Success"] = $"Đã tạo địa điểm \"{vm.Name}\"";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Locations/Form.cshtml", vm);
    }

    // GET /admin/locations/{id}/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await locationService.GetForEditAsync(id);
        if (vm == null)
        {
            TempData["Error"] = $"Không tìm thấy địa điểm với ID: {id}";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Locations/Form.cshtml", vm);
    }

    // POST /admin/locations/{id}/edit
    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LocationFormViewModel vm)
    {
        if (await locationService.UpdateAsync(id, vm, ModelState))
        {
            TempData["Success"] = $"Đã cập nhật địa điểm \"{vm.Name}\"";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Locations/Form.cshtml", vm);
    }

    // POST /admin/locations/delete (Xóa đơn lẻ hoặc xóa hàng loạt)
    [HttpPost("delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int[] ids, string? returnUrl)
    {
        if (ids.Length == 0) return RedirectBack(returnUrl);

        var (deletedCount, blockedReasons) = await locationService.DeleteLocationsAsync(ids);

        if (deletedCount > 0)
        {
            TempData["Success"] = $"Đã xóa thành công {deletedCount} địa điểm";
        }

        if (blockedReasons.Count > 0)
        {
            TempData["Error"] = "Không thể xóa do đang có tour liên kết: " + string.Join("; ", blockedReasons);
        }

        return RedirectBack(returnUrl);
    }

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) 
            ? Redirect(returnUrl) 
            : RedirectToAction(nameof(Index));
}
