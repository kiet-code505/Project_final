using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Payments;

namespace TourismMvc.Services;

public class VnpayPaymentService(AppDbContext db, IOptions<VnpayOptions> settings, TimeProvider clock, VnpayQueryClient gateway) : IVnpayPaymentService
{
    private VnpayOptions Options => settings.Value;
    private DateTime Now => PaymentTime.Now(clock);

    public async Task<BookingPaymentState> GetStateAsync(int bookingId)
    {
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingID == bookingId);
        var attempts = await db.VnpayPayments.AsNoTracking().Where(p => p.BookingId == bookingId).OrderByDescending(p => p.Id).ToListAsync();
        return new BookingPaymentState
        {
            Configured = Options.IsConfigured, Now = Now, Attempts = attempts,
            NeedsReview = attempts.Any(p => p.Status == VnpayPaymentStatus.REVIEW_REQUIRED),
            CanPay = Options.IsConfigured && booking.BookingStatus == BookingStatus.PENDING_PAYMENT && !booking.SeatsReleased
                && booking.PaymentDeadline > Now.AddMinutes(1) && booking.TotalPrice > 0
                && !attempts.Any(p => p.Status is VnpayPaymentStatus.SUCCEEDED or VnpayPaymentStatus.REVIEW_REQUIRED)
        };
    }

    // Always lock departure -> booking -> payment, matching reservation/expiry lock order.
    private async Task<Booking?> LockBookingAsync(int bookingId)
    {
        var departureId = await db.Bookings.AsNoTracking().Where(b => b.BookingID == bookingId).Select(b => (int?)b.DepartureId).SingleOrDefaultAsync();
        if (departureId == null) return null;
        await db.Departures.FromSqlInterpolated($"SELECT * FROM tour_departures WHERE departureid = {departureId.Value} FOR UPDATE").SingleAsync();
        return await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE bookingid = {bookingId} FOR UPDATE").SingleOrDefaultAsync();
    }

    public async Task<PaymentStartResult> StartAsync(int bookingId, string ipAddress)
    {
        if (!Options.IsConfigured) return new(null, "Thanh toán VNPAY đang được cấu hình. Vui lòng liên hệ hỗ trợ.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var booking = await LockBookingAsync(bookingId);
        if (booking == null) return new(null, "Không tìm thấy đơn đặt tour.");
        if (booking.BookingStatus != BookingStatus.PENDING_PAYMENT || booking.SeatsReleased)
            return new(null, "Đơn đặt tour không còn ở trạng thái chờ thanh toán.");
        if (booking.PaymentDeadline == null || booking.PaymentDeadline <= Now.AddMinutes(1))
            return new(null, "Thời gian thanh toán còn quá ngắn hoặc đã hết. Vui lòng kiểm tra lại trạng thái đơn.");
        if (booking.TotalPrice <= 0 || booking.TotalPrice != decimal.Truncate(booking.TotalPrice) || booking.TotalPrice > 9999999999m)
            return new(null, "Số tiền của đơn chưa phù hợp để thanh toán qua VNPAY. Vui lòng liên hệ hỗ trợ.");
        var attempts = await db.VnpayPayments.Where(p => p.BookingId == bookingId).OrderByDescending(p => p.Id).ToListAsync();
        if (attempts.Any(p => p.Status is VnpayPaymentStatus.SUCCEEDED or VnpayPaymentStatus.REVIEW_REQUIRED))
            return new(null, "Đơn đã có giao dịch được ghi nhận. Vui lòng kiểm tra kết quả thanh toán.");
        var payment = attempts.FirstOrDefault(p => p.Status == VnpayPaymentStatus.PENDING);
        if (payment != null && (payment.Amount != booking.TotalPrice || payment.MerchantCode != Options.TmnCode))
            return new(null, "Thông tin giao dịch đã thay đổi. Vui lòng liên hệ hỗ trợ để đối soát.");
        if (payment == null)
        {
            if (attempts.Count >= 10) return new(null, "Đơn có nhiều lần thanh toán chưa thành công. Vui lòng liên hệ hỗ trợ.");
            payment = new VnpayPayment
            {
                BookingId = bookingId, TransactionRef = "VP" + Guid.NewGuid().ToString("N"), MerchantCode = Options.TmnCode,
                Amount = booking.TotalPrice, Status = VnpayPaymentStatus.PENDING, CreatedAt = Now, ExpiresAt = booking.PaymentDeadline.Value
            };
            db.VnpayPayments.Add(payment); await db.SaveChangesAsync();
        }
        var url = BuildUrl(payment, booking.BookingCode, ipAddress);
        await transaction.CommitAsync();
        return new(url, null);
    }

    private string BuildUrl(VnpayPayment payment, string bookingCode, string ipAddress)
    {
        if (!System.Net.IPAddress.TryParse(ipAddress, out var ip)) ip = System.Net.IPAddress.Loopback;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (System.Net.IPAddress.IsLoopback(ip)) ip = System.Net.IPAddress.Loopback;
        var fields = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = payment.MerchantCode,
            ["vnp_Amount"] = checked((long)(payment.Amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND", ["vnp_TxnRef"] = payment.TransactionRef, ["vnp_OrderInfo"] = "Thanh toan tour " + bookingCode,
            ["vnp_OrderType"] = "other", ["vnp_Locale"] = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vn" : "en",
            ["vnp_ReturnUrl"] = Options.PublicBaseUrl.TrimEnd('/') + "/payment/vnpay/return",
            ["vnp_IpAddr"] = ip.ToString(), ["vnp_CreateDate"] = VnpayProtocol.Date(payment.CreatedAt), ["vnp_ExpireDate"] = VnpayProtocol.Date(payment.ExpiresAt)
        };
        var query = VnpayProtocol.Canonical(fields);
        return VnpayOptions.GatewayUrl + "?" + query + "&vnp_SecureHash=" + VnpayProtocol.Hash(Options.HashSecret, query);
    }

    private static VnpayVerifiedResult? Notification(Dictionary<string, string> values)
    {
        if (!values.TryGetValue("vnp_TxnRef", out var reference) || reference.Length > 40
            || !VnpayProtocol.TryAmount(values.GetValueOrDefault("vnp_Amount"), out var amount)
            || !values.TryGetValue("vnp_ResponseCode", out var response) || response.Length != 2
            || !values.TryGetValue("vnp_TransactionStatus", out var status) || status.Length != 2
            || values.GetValueOrDefault("vnp_TransactionNo", "").Length > 100 || values.GetValueOrDefault("vnp_BankCode", "").Length > 50
            || values.TryGetValue("vnp_CurrCode", out var currency) && currency != "VND") return null;
        return new(reference, values["vnp_TmnCode"], amount, response, status, values.GetValueOrDefault("vnp_TransactionNo"),
            values.GetValueOrDefault("vnp_BankCode"), VnpayProtocol.TryDate(values.GetValueOrDefault("vnp_PayDate"), out var date) ? date : null);
    }

    public async Task<VnpayNotificationResult> HandleIpnAsync(IQueryCollection query)
    {
        var values = VnpayProtocol.ReadQuery(query, Options);
        if (values == null) return new("97", "Invalid signature");
        var result = Notification(values);
        return result == null ? new("99", "Invalid request") : await ApplyAsync(result);
    }

    public async Task<string?> HandleReturnAsync(IQueryCollection query)
    {
        var values = VnpayProtocol.ReadQuery(query, Options);
        if (values == null || Notification(values) is not { } notice) return null;
        var payment = await db.VnpayPayments.AsNoTracking().Where(p => p.TransactionRef == notice.TransactionRef && p.MerchantCode == notice.MerchantCode)
            .Select(p => new { p.Amount, Code = p.Booking!.BookingCode }).SingleOrDefaultAsync();
        if (payment == null || payment.Amount * 100 != notice.Amount) return null;
        // A browser redirect never marks a booking paid. Query VNPAY directly when IPN is delayed (e.g. localhost).
        await RefreshAsync(payment.Code);
        return payment.Code;
    }

    private async Task<VnpayNotificationResult> ApplyAsync(VnpayVerifiedResult result)
    {
        var hint = await db.VnpayPayments.AsNoTracking().Where(p => p.TransactionRef == result.TransactionRef).Select(p => new { p.Id, p.BookingId }).SingleOrDefaultAsync();
        if (hint == null) return new("01", "Order not found");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var booking = await LockBookingAsync(hint.BookingId);
        if (booking == null) return new("01", "Order not found");
        var payment = await db.VnpayPayments.FromSqlInterpolated($"SELECT * FROM mvc_vnpay_payments WHERE id = {hint.Id} FOR UPDATE").SingleAsync();
        if (payment.MerchantCode != result.MerchantCode) return new("97", "Invalid merchant");
        if (payment.Amount * 100 != result.Amount || payment.Amount != booking.TotalPrice) return new("04", "Invalid amount");
        if (payment.Status is VnpayPaymentStatus.SUCCEEDED or VnpayPaymentStatus.REVIEW_REQUIRED) return new("02", "Order already confirmed", booking.BookingCode);
        var successful = result.ResponseCode == "00" && result.TransactionStatus == "00";
        var needsReview = result.TransactionStatus is "04" or "07" || result.ResponseCode == "07";
        if (successful && (result.PaidAt == null || result.PaidAt < payment.CreatedAt.AddMinutes(-1) || result.PaidAt > Now.AddMinutes(5)
            || !long.TryParse(result.ProviderTransactionNo, NumberStyles.None, CultureInfo.InvariantCulture, out var transactionNo) || transactionNo <= 0))
            return new("99", "Invalid settlement data");
        // Nonterminal notifications must not overwrite a definitive state or prevent a later success.
        if (!successful && !needsReview && result.TransactionStatus is not "02") return new("00", "Confirm Success", booking.BookingCode);
        payment.ProviderTransactionNo = result.ProviderTransactionNo;
        payment.BankCode = result.BankCode; payment.ResponseCode = result.ResponseCode; payment.TransactionStatus = result.TransactionStatus;
        if (successful)
        {
            payment.SettledAt = result.PaidAt;
            if (booking.BookingStatus == BookingStatus.PENDING_PAYMENT && !booking.SeatsReleased && result.PaidAt <= payment.ExpiresAt)
            {
                payment.Status = VnpayPaymentStatus.SUCCEEDED; booking.BookingStatus = BookingStatus.PAID;
                // Seats were already reserved when the booking was created. Never subtract them here.
            }
            else payment.Status = VnpayPaymentStatus.REVIEW_REQUIRED; // Late capture or a second paid attempt needs reconciliation, never oversell.
        }
        else if (needsReview) payment.Status = VnpayPaymentStatus.REVIEW_REQUIRED;
        else if (payment.Status is VnpayPaymentStatus.PENDING or VnpayPaymentStatus.EXPIRED) payment.Status = VnpayPaymentStatus.FAILED;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return new("00", "Confirm Success", booking.BookingCode);
    }

    public async Task RefreshAsync(string bookingCode, CancellationToken cancellationToken = default)
    {
        var recent = Now.AddDays(-1);
        var pending = await db.VnpayPayments.AsNoTracking().Where(p => p.Booking!.BookingCode == bookingCode
            && (p.Status == VnpayPaymentStatus.PENDING || p.Status == VnpayPaymentStatus.EXPIRED && p.CreatedAt >= recent))
            .OrderByDescending(p => p.Id).Take(10).ToListAsync(cancellationToken);
        foreach (var payment in pending)
        {
            var cutoff = Now.AddSeconds(-15);
            var claimed = await db.VnpayPayments.Where(p => p.Id == payment.Id && (p.LastCheckedAt == null || p.LastCheckedAt <= cutoff))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastCheckedAt, Now), cancellationToken);
            if (claimed == 0) continue;
            var result = await gateway.QueryAsync(payment, cancellationToken);
            if (result != null) await ApplyAsync(result);
        }
        await ExpireAsync(bookingCode);
    }

    public async Task ExpireAsync(string bookingCode)
    {
        var now = Now;
        var hint = await db.Bookings.AsNoTracking().Where(b => b.BookingCode == bookingCode && b.BookingStatus == BookingStatus.PENDING_PAYMENT
            && b.PaymentDeadline != null && b.PaymentDeadline <= now.AddMinutes(-Math.Clamp(Options.SettlementGraceMinutes, 0, 10)))
            .Select(b => (int?)b.BookingID).SingleOrDefaultAsync();
        if (hint == null) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var booking = await LockBookingAsync(hint.Value);
        if (booking == null || booking.BookingStatus != BookingStatus.PENDING_PAYMENT || booking.SeatsReleased
            || booking.PaymentDeadline > now.AddMinutes(-Math.Clamp(Options.SettlementGraceMinutes, 0, 10))) return;
        var seats = booking.ReservedSeats ?? await db.BookingPassengers.CountAsync(p => p.BookingId == booking.BookingID && p.PassengerType != "INFANT");
        var departure = await db.Departures.SingleAsync(d => d.DepartureID == booking.DepartureId);
        departure.AvailableSlots = checked(departure.AvailableSlots + seats);
        booking.SeatsReleased = true; booking.BookingStatus = BookingStatus.EXPIRED;
        var pending = await db.VnpayPayments.Where(p => p.BookingId == booking.BookingID && p.Status == VnpayPaymentStatus.PENDING).ToListAsync();
        foreach (var payment in pending) payment.Status = VnpayPaymentStatus.EXPIRED;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }
}
