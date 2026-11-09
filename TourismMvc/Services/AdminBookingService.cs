using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Payments;

namespace TourismMvc.Services;

public class AdminBookingService(AppDbContext db, TimeProvider clock) : IAdminBookingService
{
    private DateTime Now => PaymentTime.Now(clock);

    public async Task<AdminBookingIndexViewModel> SearchAsync(AdminBookingFilter filter)
    {
        filter.Search = filter.Search?.Trim();
        filter.PageSize = filter.PageSize is 10 or 20 or 50 ? filter.PageSize : 10;
        if (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value)) filter.Status = null;
        var stats = await db.Bookings.AsNoTracking().GroupBy(b => 1).Select(g => new
        {
            Total = g.Count(), Pending = g.Count(b => b.BookingStatus == BookingStatus.PENDING_PAYMENT || b.BookingStatus == BookingStatus.PENDING_CONFIRMATION),
            Paid = g.Count(b => b.BookingStatus == BookingStatus.PAID), Closed = g.Count(b => b.BookingStatus == BookingStatus.CANCELLED || b.BookingStatus == BookingStatus.EXPIRED)
        }).SingleOrDefaultAsync();
        var query = db.Bookings.AsNoTracking();
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var term = filter.Search.ToLower();
            query = query.Where(b => b.BookingCode.ToLower().Contains(term) || b.ContactFullName.ToLower().Contains(term)
                || b.ContactEmail.ToLower().Contains(term) || b.ContactPhone.Contains(term)
                || b.TourDeparture!.Tour!.TourName.ToLower().Contains(term) || b.TourDeparture.Tour.TourCode.ToLower().Contains(term));
        }
        if (filter.Status.HasValue) query = query.Where(b => b.BookingStatus == filter.Status.Value);
        if (filter.FromDate.HasValue)
        {
            var start = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(b => b.BookingDate >= start);
        }
        if (filter.ToDate.HasValue)
        {
            var end = filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(b => b.BookingDate <= end);
        }
        var count = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling((double)count / filter.PageSize)));
        return new AdminBookingIndexViewModel
        {
            Filter = filter, TotalItems = count, StatTotal = stats?.Total ?? 0, StatPending = stats?.Pending ?? 0,
            StatPaid = stats?.Paid ?? 0, StatClosed = stats?.Closed ?? 0,
            Items = await query.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.BookingID)
                .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(b => new AdminBookingListItem
                {
                    Id = b.BookingID, Code = b.BookingCode, ContactName = b.ContactFullName, ContactEmail = b.ContactEmail,
                    ContactPhone = b.ContactPhone, BookingDate = b.BookingDate, Status = b.BookingStatus,
                    TotalPassengers = b.TotalPassengers, TotalPrice = b.TotalPrice,
                    TourName = b.TourDeparture!.Tour!.TourName, TourCode = b.TourDeparture.Tour.TourCode, DepartureDate = b.TourDeparture.DepartureDate
                }).ToListAsync()
        };
    }

    public async Task<AdminBookingDetailsViewModel?> GetDetailsAsync(int id)
    {
        var booking = await db.Bookings.AsNoTracking().Where(b => b.BookingID == id)
            .Include(b => b.Passengers).Include(b => b.Payments)
            .Include(b => b.TourDeparture).ThenInclude(d => d!.Tour).ThenInclude(t => t!.StartLocation)
            .Include(b => b.TourDeparture).ThenInclude(d => d!.Tour).ThenInclude(t => t!.EndLocation)
            .Include(b => b.TourDeparture).ThenInclude(d => d!.Transports).AsSplitQuery().SingleOrDefaultAsync();
        if (booking == null) return null;
        var account = booking.UserId.HasValue ? await db.Users.AsNoTracking().Where(u => u.UserID == booking.UserId)
            .Select(u => new { u.FullName, u.Email }).SingleOrDefaultAsync() : null;
        return new AdminBookingDetailsViewModel
        {
            Booking = booking, AccountName = account?.FullName, AccountEmail = account?.Email,
            CancelBlockedReason = CancellationError(booking, Now), ConfirmBlockedReason = ConfirmationError(booking, Now)
        };
    }

    private static string? CancellationError(Booking booking, DateTime now)
    {
        if (booking.BookingStatus == BookingStatus.PAID || booking.Payments.Any(p => p.Status is VnpayPaymentStatus.SUCCEEDED or VnpayPaymentStatus.REVIEW_REQUIRED))
            return "Đơn đã có thanh toán hoặc giao dịch cần đối soát, không thể hủy tại đây.";
        if (booking.BookingStatus is not (BookingStatus.PENDING_PAYMENT or BookingStatus.PENDING_CONFIRMATION))
            return "Chỉ có thể hủy đơn đang chờ thanh toán hoặc chờ xác nhận.";
        if (booking.TourDeparture == null || booking.TourDeparture.DepartureDate <= now) return "Chuyến đi đã khởi hành, không thể hủy đơn.";
        return null;
    }

    private static string? ConfirmationError(Booking booking, DateTime now)
    {
        if (booking.BookingStatus != BookingStatus.PENDING_CONFIRMATION) return "Chỉ xác nhận thu tiền cho đơn đang chờ xác nhận.";
        if (booking.SeatsReleased) return "Đơn không còn giữ chỗ, không thể xác nhận thu tiền.";
        if (booking.Payments.Count != 0) return "Đơn có giao dịch VNPAY cần được xác nhận qua kết quả thanh toán.";
        if (booking.TourDeparture == null || booking.TourDeparture.DepartureDate <= now) return "Chuyến đi đã khởi hành, không thể xác nhận đơn tại đây.";
        return null;
    }

    public Task<(bool Success, string Message)> CancelAsync(int id, BookingStatus expectedStatus) => ChangeAsync(id, expectedStatus, false);
    public Task<(bool Success, string Message)> ConfirmPaymentAsync(int id, BookingStatus expectedStatus) => ChangeAsync(id, expectedStatus, true);

    private async Task<(bool Success, string Message)> ChangeAsync(int id, BookingStatus expectedStatus, bool confirmPayment)
    {
        if (!Enum.IsDefined(expectedStatus)) return (false, "Trạng thái đơn không hợp lệ.");
        var hint = await db.Bookings.AsNoTracking().Where(b => b.BookingID == id).Select(b => new { b.DepartureId }).SingleOrDefaultAsync();
        if (hint == null) return (false, "Không tìm thấy booking. Vui lòng tải lại danh sách.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Same lock order as VNPAY and expiry: departure -> booking -> payments.
        var departure = await db.Departures.FromSqlInterpolated($"SELECT * FROM tour_departures WHERE departureid = {hint.DepartureId} FOR UPDATE").SingleOrDefaultAsync();
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE bookingid = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (booking == null || departure == null) return (false, "Booking hoặc lịch khởi hành không còn tồn tại.");
        if (!confirmPayment && booking.BookingStatus == BookingStatus.CANCELLED) return (true, "Booking đã được hủy trước đó.");
        if (confirmPayment && booking.BookingStatus == BookingStatus.PAID) return (true, "Booking đã được thanh toán trước đó.");
        if (booking.BookingStatus != expectedStatus) return (false, "Trạng thái booking vừa thay đổi. Vui lòng xem lại đơn trước khi thao tác.");
        booking.TourDeparture = departure;
        booking.Payments = await db.VnpayPayments.Where(p => p.BookingId == id).ToListAsync();
        var error = confirmPayment ? ConfirmationError(booking, Now) : CancellationError(booking, Now);
        if (error != null) return (false, error);
        if (confirmPayment) booking.BookingStatus = BookingStatus.PAID;
        else
        {
            if (!booking.SeatsReleased)
            {
                var seats = booking.ReservedSeats ?? await db.BookingPassengers.CountAsync(p => p.BookingId == id && p.PassengerType != "INFANT");
                departure.AvailableSlots = checked(departure.AvailableSlots + seats);
                booking.SeatsReleased = true;
            }
            booking.BookingStatus = BookingStatus.CANCELLED;
            foreach (var payment in booking.Payments.Where(p => p.Status == VnpayPaymentStatus.PENDING)) payment.Status = VnpayPaymentStatus.EXPIRED;
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return (true, confirmPayment ? "Đã xác nhận thu tiền cho booking." : "Đã hủy booking và trả lại chỗ đã giữ.");
    }
}
