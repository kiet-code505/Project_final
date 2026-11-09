namespace TourismMvc.Payments;

public class VnpayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "http://localhost:5019";
    public int HoldMinutes { get; set; } = 15;
    public int SettlementGraceMinutes { get; set; } = 2;
    public const string GatewayUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public const string QueryUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public bool IsConfigured => TmnCode.Length is > 0 and <= 32 && TmnCode.All(char.IsAsciiLetterOrDigit)
        && HashSecret.Length >= 16 && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}

public static class PaymentTime
{
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    public static DateTime Now(TimeProvider clock) => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Vietnam).DateTime;
}
