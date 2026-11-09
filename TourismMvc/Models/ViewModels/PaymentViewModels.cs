using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class BookingPaymentState
{
    public bool Configured { get; set; }
    public bool CanPay { get; set; }
    public bool NeedsReview { get; set; }
    public DateTime Now { get; set; }
    public List<VnpayPayment> Attempts { get; set; } = new();
}

public record PaymentStartResult(string? Url, string? Error);
public record VnpayNotificationResult(string RspCode, string Message, string? BookingCode = null);
public record VnpayVerifiedResult(string TransactionRef, string MerchantCode, long Amount, string ResponseCode,
    string TransactionStatus, string? ProviderTransactionNo, string? BankCode, DateTime? PaidAt);
