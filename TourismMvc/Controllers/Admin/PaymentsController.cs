using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;

namespace TourismMvc.Controllers.Admin;

[Route("admin/payments"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PaymentsController(AppDbContext db, IVnpayPaymentService payments) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, VnpayPaymentStatus? status, int page = 1)
    {
        search = search?.Trim();
        if (status.HasValue && !Enum.IsDefined(status.Value)) status = null;
        var query = db.VnpayPayments.AsNoTracking();
        if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.TransactionRef.Contains(search) || p.Booking!.BookingCode.Contains(search)
            || p.Booking.ContactEmail.ToLower().Contains(search.ToLower()));
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / 20d)));
        return View("~/Views/Admin/Payments/Index.cshtml", new AdminPaymentViewModel
        {
            Search = search, Status = status, Page = page, TotalItems = count,
            ReviewCount = await db.VnpayPayments.CountAsync(p => p.Status == VnpayPaymentStatus.REVIEW_REQUIRED),
            Items = await query.OrderByDescending(p => p.Status == VnpayPaymentStatus.REVIEW_REQUIRED).ThenByDescending(p => p.Id)
                .Skip((page - 1) * 20).Take(20).Include(p => p.Booking).ToListAsync()
        });
    }

    [HttpPost("{id:long}/refresh"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(long id)
    {
        var code = await db.VnpayPayments.AsNoTracking().Where(p => p.Id == id).Select(p => p.Booking!.BookingCode).SingleOrDefaultAsync();
        if (code == null) TempData["Error"] = "Không tìm thấy giao dịch.";
        else { await payments.RefreshAsync(code, HttpContext.RequestAborted); TempData["Success"] = "Đã kiểm tra giao dịch với VNPAY."; }
        return RedirectToAction(nameof(Index));
    }
}
