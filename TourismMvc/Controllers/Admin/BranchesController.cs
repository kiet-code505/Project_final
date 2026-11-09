using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

[Route("admin/branches")]
public class BranchesController(IBranchContactService branchService) : AdminControllerBase
{
    // GET /admin/branches
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 12)
    {
        var vm = await branchService.GetPagedBranchesAsync(search, page, pageSize);
        return View("~/Views/Admin/Branches/Index.cshtml", vm);
    }

    // GET /admin/branches/create
    [HttpGet("create")]
    public IActionResult Create() => View("~/Views/Admin/Branches/Form.cshtml", new BranchFormViewModel());

    // POST /admin/branches/create
    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BranchFormViewModel vm)
    {
        if (await branchService.CreateAsync(vm, ModelState))
        {
            TempData["Success"] = $"Đã tạo chi nhánh \"{vm.BranchName}\"";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Branches/Form.cshtml", vm);
    }

    // GET /admin/branches/{id}/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await branchService.GetForEditAsync(id);
        if (vm == null)
        {
            TempData["Error"] = $"Không tìm thấy chi nhánh với ID: {id}";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Branches/Form.cshtml", vm);
    }

    // POST /admin/branches/{id}/edit
    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BranchFormViewModel vm)
    {
        if (await branchService.UpdateAsync(id, vm, ModelState))
        {
            TempData["Success"] = $"Đã cập nhật chi nhánh \"{vm.BranchName}\"";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Branches/Form.cshtml", vm);
    }

    // POST /admin/branches/{id}/delete
    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var (success, errorMessage) = await branchService.DeleteAsync(id);
        if (success)
        {
            TempData["Success"] = "Đã xóa chi nhánh thành công.";
        }
        else
        {
            TempData["Error"] = errorMessage ?? "Không thể xóa chi nhánh.";
        }

        return RedirectBack(returnUrl);
    }

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
