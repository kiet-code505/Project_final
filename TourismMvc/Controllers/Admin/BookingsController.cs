using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

[Route("admin/bookings"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BookingsController(IAdminBookingService bookings, IVnpayPaymentService payments) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminBookingFilter filter)
    {
        if (filter.FromDate > filter.ToDate) ModelState.AddModelError("ToDate", "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        return View("~/Views/Admin/Bookings/Index.cshtml", await bookings.SearchAsync(filter));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, string? returnUrl)
    {
        var vm = await bookings.GetDetailsAsync(id);
        if (vm == null) return Missing();
        vm.ReturnUrl = SafeReturnUrl(returnUrl);
        return View("~/Views/Admin/Bookings/Details.cshtml", vm);
    }

    [HttpGet("{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, string? returnUrl) => ActionPage(id, returnUrl, false);

    [HttpGet("{id:int}/confirm")]
    public Task<IActionResult> Confirm(int id, string? returnUrl) => ActionPage(id, returnUrl, true);

    private async Task<IActionResult> ActionPage(int id, string? returnUrl, bool confirm)
    {
        var vm = await bookings.GetDetailsAsync(id);
        if (vm == null) return Missing();
        vm.ReturnUrl = SafeReturnUrl(returnUrl);
        return View("~/Views/Admin/Bookings/Action.cshtml", new AdminBookingActionViewModel { Details = vm, ConfirmPayment = confirm });
    }

    [HttpPost("{id:int}/cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(int id, AdminBookingActionInput input) => Change(id, input, false);

    [HttpPost("{id:int}/confirm"), ValidateAntiForgeryToken]
    public Task<IActionResult> Confirm(int id, AdminBookingActionInput input) => Change(id, input, true);

    private async Task<IActionResult> Change(int id, AdminBookingActionInput input, bool confirm)
    {
        if (!ModelState.IsValid || !input.ExpectedStatus.HasValue)
        {
            TempData["Error"] = "Thông tin xác nhận chưa hợp lệ. Vui lòng mở lại booking và thử lại.";
            return Redirect(SafeReturnUrl(input.ReturnUrl));
        }
        var result = confirm ? await bookings.ConfirmPaymentAsync(id, input.ExpectedStatus.Value) : await bookings.CancelAsync(id, input.ExpectedStatus.Value);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return Redirect(SafeReturnUrl(input.ReturnUrl));
    }

    [HttpPost("{id:int}/refresh-payment"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPayment(int id)
    {
        var vm = await bookings.GetDetailsAsync(id);
        if (vm == null) return Missing();
        await payments.RefreshAsync(vm.Booking.BookingCode, HttpContext.RequestAborted);
        TempData["Success"] = "Đã kiểm tra lại trạng thái giao dịch với VNPAY.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private string SafeReturnUrl(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url)
        && (url.Split('?', '#')[0].Equals("/admin/bookings", StringComparison.OrdinalIgnoreCase) || url.StartsWith("/admin/bookings/", StringComparison.OrdinalIgnoreCase))
        ? url : "/admin/bookings";

    private IActionResult Missing()
    {
        TempData["Error"] = "Không tìm thấy booking. Đơn có thể đã được thay đổi hoặc xóa.";
        return RedirectToAction(nameof(Index));
    }
}
