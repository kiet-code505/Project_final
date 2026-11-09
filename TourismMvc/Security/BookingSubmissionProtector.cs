using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Security;

public class BookingSubmissionProtector(IDataProtectionProvider provider)
{
    private const string BrowserCookie = ".TourismMvc.BookingBrowser";
    private readonly ITimeLimitedDataProtector submissionProtector = provider.CreateProtector("TourismMvc.BookingSubmission.v1").ToTimeLimitedDataProtector();
    private readonly ITimeLimitedDataProtector receiptProtector = provider.CreateProtector("TourismMvc.BookingReceipt.v1").ToTimeLimitedDataProtector();
    public string GetBrowserId(HttpContext context)
    {
        var existing = context.Request.Cookies[BrowserCookie];
        if (!string.IsNullOrEmpty(existing) && existing.Length == 32 && existing.All(Uri.IsHexDigit)) return existing;
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        context.Response.Cookies.Append(BrowserCookie, id, Options(context, "/", TimeSpan.FromDays(30)));
        return id;
    }
    public string Create(BookingTourInfo info, int? userId, string browserId) => submissionProtector.Protect(
        JsonSerializer.Serialize(new BookingSubmission("BK" + Convert.ToHexString(RandomNumberGenerator.GetBytes(9)), info.Departure.DepartureID, info.QuoteStamp, userId, browserId)), TimeSpan.FromHours(2));
    public BookingSubmission? Read(string token, int departureId, int? userId, string browserId)
    {
        try
        {
            var data = JsonSerializer.Deserialize<BookingSubmission>(submissionProtector.Unprotect(token));
            return data != null && data.DepartureId == departureId && data.UserId == userId && data.BrowserId == browserId ? data : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException) { return null; }
    }
    public void GrantGuestReceipt(HttpContext context, string code, string browserId) => context.Response.Cookies.Append(
        ".TourismMvc.Receipt." + code, receiptProtector.Protect(code + ":" + browserId, TimeSpan.FromDays(30)),
        Options(context, "/booking/complete/" + code, TimeSpan.FromDays(30)));
    public bool CanReadGuestReceipt(HttpContext context, string code)
    {
        var token = context.Request.Cookies[".TourismMvc.Receipt." + code];
        var browserId = context.Request.Cookies[BrowserCookie];
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(browserId)) return false;
        try { return receiptProtector.Unprotect(token) == code + ":" + browserId; }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException) { return false; }
    }
    private static CookieOptions Options(HttpContext context, string path, TimeSpan duration) => new()
    {
        HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax,
        Path = path, IsEssential = true, Expires = DateTimeOffset.UtcNow.Add(duration)
    };
}
