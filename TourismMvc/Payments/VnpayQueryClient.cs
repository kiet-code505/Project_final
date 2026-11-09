using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Payments;

public class VnpayQueryClient(HttpClient http, IOptions<VnpayOptions> settings, TimeProvider clock, ILogger<VnpayQueryClient> logger)
{
    public async Task<VnpayVerifiedResult?> QueryAsync(VnpayPayment payment, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        if (!options.IsConfigured || options.TmnCode != payment.MerchantCode) return null;
        var values = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = Guid.NewGuid().ToString("N"), ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "querydr",
            ["vnp_TmnCode"] = payment.MerchantCode, ["vnp_TxnRef"] = payment.TransactionRef,
            ["vnp_OrderInfo"] = "Query payment " + payment.TransactionRef, ["vnp_TransactionDate"] = VnpayProtocol.Date(payment.CreatedAt),
            ["vnp_CreateDate"] = VnpayProtocol.Date(PaymentTime.Now(clock)), ["vnp_IpAddr"] = "127.0.0.1"
        };
        var requestFields = new[] { "vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TxnRef", "vnp_TransactionDate", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo" };
        values["vnp_SecureHash"] = VnpayProtocol.Hash(options.HashSecret, string.Join("|", requestFields.Select(k => values[k])));
        try
        {
            using var response = await http.PostAsJsonAsync(VnpayOptions.QueryUrl, values, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("VNPAY query returned HTTP {StatusCode} for transaction {Reference}", (int)response.StatusCode, payment.TransactionRef);
                return null;
            }
            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(cancellationToken);
            if (data == null) return null;
            string Field(string key) => data.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";
            if (Field("vnp_ResponseCode") != "00")
            {
                var code = Field("vnp_ResponseCode");
                logger.LogWarning("VNPAY query returned response code {Code} without a verified settlement for transaction {Reference}",
                    code.Length == 2 && code.All(char.IsAsciiDigit) ? code : "unknown", payment.TransactionRef);
                return null;
            }
            var responseFields = new[] { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode", "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo", "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo", "vnp_PromotionCode", "vnp_PromotionAmount" };
            if (!VnpayProtocol.Matches(Field("vnp_SecureHash"), VnpayProtocol.Hash(options.HashSecret, string.Join("|", responseFields.Select(Field))))
                || Field("vnp_TmnCode") != payment.MerchantCode || Field("vnp_TxnRef") != payment.TransactionRef || Field("vnp_Command") != "querydr"
                || Field("vnp_ResponseCode") != "00" || !VnpayProtocol.TryAmount(Field("vnp_Amount"), out var amount)) return null;
            var status = Field("vnp_TransactionStatus");
            if (status is not ("00" or "02" or "04" or "07")) return null; // Pending/unknown is not a definitive failure.
            return new VnpayVerifiedResult(payment.TransactionRef, payment.MerchantCode, amount, status == "00" ? "00" : "99", status,
                Field("vnp_TransactionNo"), Field("vnp_BankCode"), VnpayProtocol.TryDate(Field("vnp_PayDate"), out var date) ? date : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            // Do not log signed payloads, merchant keys or customer details.
            logger.LogWarning("VNPAY reconciliation unavailable for transaction {Reference}: {ErrorType}", payment.TransactionRef, ex.GetType().Name);
            return null;
        }
    }
}
