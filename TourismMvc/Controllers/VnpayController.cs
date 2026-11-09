using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismMvc.Services;

namespace TourismMvc.Controllers;

[AllowAnonymous, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class VnpayController(IVnpayPaymentService payments, ILogger<VnpayController> logger) : Controller
{
    [HttpGet("/payment/vnpay/ipn")]
    public async Task<IActionResult> Ipn()
    {
        try
        {
            var result = await payments.HandleIpnAsync(Request.Query);
            return Json(new { result.RspCode, result.Message });
        }
        catch (Exception ex)
        {
            logger.LogError("VNPAY IPN processing failed: {ErrorType}", ex.GetType().Name);
            return Json(new { RspCode = "99", Message = "Please retry" });
        }
    }

    [HttpGet("/payment/vnpay/return")]
    public async Task<IActionResult> Return()
    {
        var code = await payments.HandleReturnAsync(Request.Query);
        if (code == null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("~/Views/Booking/PaymentError.cshtml");
        }
        TempData["PaymentNotice"] = "Đã nhận kết quả từ VNPAY. Trạng thái bên dưới được xác nhận bằng thông báo hoặc truy vấn trực tiếp đến VNPAY.";
        return RedirectToAction("Complete", "Booking", new { code });
    }
}
