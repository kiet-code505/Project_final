using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

[Route("admin/users"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class UsersController(IAdminUserService users) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminUserFilter filter, string? search)
    {
        filter.Email ??= search; // Keep links using the original frontend's ?search=email format.
        return View("~/Views/Admin/Users/Index.cshtml", await users.SearchAsync(filter));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, int page = 1, string? returnUrl = null)
    {
        var vm = await users.GetDetailsAsync(id, page);
        if (vm == null) return MissingAccount();
        vm.ReturnUrl = SafeReturnUrl(returnUrl);
        return View("~/Views/Admin/Users/Details.cshtml", vm);
    }

    [HttpGet("{id:int}/status")]
    public async Task<IActionResult> Status(int id, string? returnUrl = null)
    {
        var account = await users.GetAsync(id);
        if (account == null) return MissingAccount();
        return View("~/Views/Admin/Users/Status.cshtml", new AdminUserStatusViewModel
        {
            Account = account, Status = !account.Status, ReturnUrl = SafeReturnUrl(returnUrl)
        });
    }

    [HttpPost("{id:int}/status"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int id, AdminUserStatusInput input)
    {
        var back = SafeReturnUrl(input.ReturnUrl);
        if (!ModelState.IsValid || !input.Status.HasValue)
        {
            TempData["Error"] = "Trạng thái tài khoản không hợp lệ. Vui lòng thử lại.";
            return Redirect(back);
        }
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Forbid();
        var (success, message) = await users.SetStatusAsync(id, input.Status.Value, actorId);
        TempData[success ? "Success" : "Error"] = message;
        return Redirect(back);
    }

    private string SafeReturnUrl(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url)
        && (url.Split('?', '#')[0].Equals("/admin/users", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("/admin/users/", StringComparison.OrdinalIgnoreCase)) ? url : "/admin/users";

    private IActionResult MissingAccount()
    {
        TempData["Error"] = "Không tìm thấy tài khoản. Tài khoản có thể đã được thay đổi hoặc xóa.";
        return RedirectToAction(nameof(Index));
    }
}
