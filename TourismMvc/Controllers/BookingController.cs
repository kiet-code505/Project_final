using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Security;
using TourismMvc.Services;

namespace TourismMvc.Controllers;

[AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class BookingController(IBookingOrderService bookingService, BookingSubmissionProtector protector, IVnpayPaymentService payments) : Controller
{
    private int? CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("/order-booking")]
    public async Task<IActionResult> Order(string? tourCode, int? departureId)
    {
        if (string.IsNullOrWhiteSpace(tourCode) || departureId is not > 0) return Unavailable("Vui lòng chọn tour và lịch khởi hành trước khi đặt chỗ.", 400);
        var info = await bookingService.GetInfoAsync(tourCode, departureId.Value);
        if (info == null) return Unavailable("Không tìm thấy tour và lịch khởi hành phù hợp.", 404);
        var browserId = protector.GetBrowserId(HttpContext);
        var input = new BookingInputModel { TourCode = info.TourCode, DepartureId = departureId.Value, SubmissionToken = protector.Create(info, CurrentUserId, browserId) };
        var vm = new BookingOrderViewModel { Info = info, Input = input };
        if (CurrentUserId.HasValue) vm.ContactPrefilled = await bookingService.PrefillAsync(input, CurrentUserId.Value);
        return View(vm);
    }

    [HttpPost("/order-booking"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Order([Bind(Prefix = "Input")] BookingInputModel input, string? intent)
    {
        if (string.IsNullOrWhiteSpace(input.TourCode) || input.DepartureId <= 0) return Unavailable("Thông tin lịch khởi hành chưa hợp lệ.", 400);
        var info = await bookingService.GetInfoAsync(input.TourCode, input.DepartureId);
        if (info == null) return Unavailable("Lịch khởi hành không còn hiển thị. Vui lòng chọn lịch khác.", 404);
        var browserId = protector.GetBrowserId(HttpContext);
        var submission = protector.Read(input.SubmissionToken ?? "", input.DepartureId, CurrentUserId, browserId);
        if (intent != null)
        {
            UpdatePassengers(input, intent, info);
            ModelState.Clear();
        }
        else if (submission == null)
            ModelState.AddModelError("", "Phiên đặt tour đã hết hạn hoặc tài khoản đã thay đổi. Vui lòng kiểm tra thông tin và gửi lại.");
        else if (ModelState.IsValid)
        {
            var booking = await bookingService.CreateAsync(input, submission, ModelState);
            if (booking != null)
            {
                if (booking.UserId == null) protector.GrantGuestReceipt(HttpContext, booking.BookingCode, browserId);
                return RedirectToAction(nameof(Complete), new { code = booking.BookingCode });
            }
            info = await bookingService.GetInfoAsync(input.TourCode, input.DepartureId) ?? info;
        }
        // Keep the original nonce for ordinary validation retries; issue a fresh quote after changes.
        input.SubmissionToken = submission != null && submission.QuoteStamp == info.QuoteStamp
            ? input.SubmissionToken! : protector.Create(info, CurrentUserId, browserId);
        ModelState.Remove("Input.SubmissionToken");
        return View(new BookingOrderViewModel { Info = info, Input = input });
    }

    [HttpGet("/booking/complete/{code}")]
    public async Task<IActionResult> Complete(string code)
    {
        var booking = await AccessibleBookingAsync(code);
        if (booking == null) return NotFound();
        return View(new BookingConfirmationViewModel { Booking = booking, Payment = await payments.GetStateAsync(booking.BookingID) });
    }

    [HttpPost("/booking/complete/{code}/pay"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string code)
    {
        var booking = await AccessibleBookingAsync(code);
        if (booking == null) return NotFound();
        var result = await payments.StartAsync(booking.BookingID, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
        if (result.Url != null) return Redirect(result.Url);
        TempData["PaymentError"] = result.Error;
        return RedirectToAction(nameof(Complete), new { code });
    }

    [HttpPost("/booking/complete/{code}/refresh"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPayment(string code)
    {
        if (await AccessibleBookingAsync(code) == null) return NotFound();
        await payments.RefreshAsync(code, HttpContext.RequestAborted);
        TempData["PaymentNotice"] = "Đã kiểm tra trạng thái thanh toán. Nếu bạn vừa thanh toán, kết quả có thể cần thêm thời gian để cập nhật.";
        return RedirectToAction(nameof(Complete), new { code });
    }

    [HttpGet("/booking/complete/{code}/status")]
    public async Task<IActionResult> PaymentStatus(string code)
    {
        var booking = await AccessibleBookingAsync(code);
        if (booking == null) return NotFound();
        var state = await payments.GetStateAsync(booking.BookingID);
        return Json(new { status = booking.BookingStatus.ToString(), needsReview = state.NeedsReview });
    }

    private async Task<TourismMvc.Models.Entities.Booking?> AccessibleBookingAsync(string code)
    {
        if (code.Length != 20 || !code.StartsWith("BK", StringComparison.Ordinal) || !code[2..].All(Uri.IsHexDigit)) return null;
        var guestAccess = protector.CanReadGuestReceipt(HttpContext, code);
        if (!CurrentUserId.HasValue && !guestAccess) return null;
        var booking = await bookingService.GetConfirmationAsync(code);
        return booking != null && (booking.UserId != null && booking.UserId == CurrentUserId || booking.UserId == null && guestAccess) ? booking : null;
    }

    private IActionResult Unavailable(string message, int status)
    {
        Response.StatusCode = status; ViewData["Message"] = message;
        return View("Unavailable");
    }

    private static void UpdatePassengers(BookingInputModel input, string intent, BookingTourInfo info)
    {
        if (intent.StartsWith("add:", StringComparison.Ordinal) && input.Passengers.Count < 50)
        {
            var type = intent[4..];
            if (type is "ADULT" or "CHILD" or "INFANT" && info.Prices.Any(p => p.PassengerType.ToString() == type))
                input.Passengers.Add(new BookingPassengerInput { PassengerType = type });
        }
        else if (intent.StartsWith("remove:", StringComparison.Ordinal) && int.TryParse(intent[7..], out var index)
            && index >= 0 && index < input.Passengers.Count && input.Passengers.Count > 1)
            input.Passengers.RemoveAt(index);
    }
}
