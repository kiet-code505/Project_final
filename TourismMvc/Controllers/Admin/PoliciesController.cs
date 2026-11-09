using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

[Route("admin/policies")]
public class PoliciesController(
    IPolicyTemplateService policyService,
    IBranchContactService branchService) : AdminControllerBase
{
    // GET /admin/policies
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int? contactId, int page = 1, int pageSize = 10)
    {
        var vm = await policyService.GetPagedPoliciesAsync(search, contactId, page, pageSize);
        return View("~/Views/Admin/Policies/Index.cshtml", vm);
    }

    // GET /admin/policies/create
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var branches = await branchService.GetAllActiveBranchesAsync();
        return View("~/Views/Admin/Policies/Form.cshtml", new PolicyFormViewModel
        {
            AvailableBranches = branches
        });
    }

    // POST /admin/policies/create
    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PolicyFormViewModel vm)
    {
        if (await policyService.CreateAsync(vm, ModelState))
        {
            TempData["Success"] = $"Đã tạo mẫu chính sách \"{vm.TemplateName}\"";
            return RedirectToAction(nameof(Index));
        }

        vm.AvailableBranches = await branchService.GetAllActiveBranchesAsync();
        return View("~/Views/Admin/Policies/Form.cshtml", vm);
    }

    // GET /admin/policies/{id}/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await policyService.GetForEditAsync(id);
        if (vm == null)
        {
            TempData["Error"] = $"Không tìm thấy mẫu chính sách với ID: {id}";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Admin/Policies/Form.cshtml", vm);
    }

    // POST /admin/policies/{id}/edit
    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PolicyFormViewModel vm)
    {
        if (await policyService.UpdateAsync(id, vm, ModelState))
        {
            TempData["Success"] = $"Đã cập nhật mẫu chính sách \"{vm.TemplateName}\"";
            return RedirectToAction(nameof(Index));
        }

        vm.AvailableBranches = await branchService.GetAllActiveBranchesAsync();
        return View("~/Views/Admin/Policies/Form.cshtml", vm);
    }

    // POST /admin/policies/{id}/delete
    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var (success, errorMessage) = await policyService.DeleteAsync(id);
        if (success)
        {
            TempData["Success"] = "Đã xóa mẫu chính sách thành công.";
        }
        else
        {
            TempData["Error"] = errorMessage ?? "Không thể xóa mẫu chính sách.";
        }

        return RedirectBack(returnUrl);
    }

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
