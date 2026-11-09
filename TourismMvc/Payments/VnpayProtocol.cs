using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TourismMvc.Payments;

public static class VnpayProtocol
{
    // VNPAY 2.1.0: sort ordinally, URL-encode values, then HMAC-SHA512 the same canonical query.
    public static string Canonical(IEnumerable<KeyValuePair<string, string>> values) => string.Join("&", values
        .Where(x => !string.IsNullOrEmpty(x.Value) && x.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
        .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value)));
    public static string Hash(string secret, string text) => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static bool Matches(string actual, string expected)
    {
        if (actual.Length != 128 || !actual.All(Uri.IsHexDigit)) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected));
    }
    public static Dictionary<string, string>? ReadQuery(IQueryCollection query, VnpayOptions options)
    {
        if (!options.IsConfigured) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in query.Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal)))
        {
            if (entry.Value.Count != 1 || entry.Key.Length > 100 || entry.Value[0]?.Length > 1000) return null;
            values.Add(entry.Key, entry.Value[0] ?? "");
        }
        if (!values.TryGetValue("vnp_SecureHash", out var signature) || !Matches(signature, Hash(options.HashSecret, Canonical(values)))
            || values.GetValueOrDefault("vnp_TmnCode") != options.TmnCode) return null;
        return values;
    }
    public static string Date(DateTime value) => value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    public static bool TryDate(string? value, out DateTime result) => DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    public static bool TryAmount(string? value, out long amount) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0;
}
