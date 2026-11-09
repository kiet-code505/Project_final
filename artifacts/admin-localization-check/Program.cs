using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using TourismMvc.Controllers;
using TourismMvc.Localization;
using TourismMvc.Models.ViewModels;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../TourismMvc"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, ApplicationName = typeof(LanguageController).Assembly.GetName().Name });
var address = args.Contains("--serve") ? "http://127.0.0.1:5047" : "http://127.0.0.1:5048";
builder.WebHost.UseUrls(address);
builder.Logging.ClearProviders();
builder.Services.AddSingleton<UserTranslationCatalog>();
builder.Services.AddScoped<UserText>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddControllersWithViews(o => o.Filters.Add<UserLocalizationFilter>())
    .AddApplicationPart(typeof(PreviewController).Assembly);
await using var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(new RequestLocalizationOptions {
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = [CultureInfo.GetCultureInfo("en"), CultureInfo.GetCultureInfo("vi")],
    SupportedUICultures = [CultureInfo.GetCultureInfo("en"), CultureInfo.GetCultureInfo("vi")],
    RequestCultureProviders = [new CookieRequestCultureProvider { CookieName = ".TourismMvc.Language" }]
});
app.Use(async (context, next) => {
    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Preview Admin"), new Claim(ClaimTypes.NameIdentifier, "999"), new Claim(ClaimTypes.Role, "ADMIN")], "Preview"));
    await next();
});
app.MapControllers();
app.MapControllerRoute("default", "{controller=Preview}/{action=Index}/{id?}");
await app.StartAsync();
if (args.Contains("--serve")) { Console.WriteLine("Isolated in-memory preview: http://127.0.0.1:5047/admin/localization-preview/Locations/Index"); await app.WaitForShutdownAsync(); return; }
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi-VN");
var pages = new[] { "Locations/Index", "Locations/Form", "Branches/Index", "Branches/Form", "Policies/Index", "Policies/Form", "Tours/Index", "Tours/Form", "Tours/Details", "Departures/Index", "Departures/Form", "Departures/Details", "Departures/Clone", "Users/Index", "Users/Details", "Users/Status", "Bookings/Index", "Bookings/Details", "Bookings/Action", "Payments/Index", "Account/Login" };
foreach (var language in new[] { "en", "vi" }) {
    foreach (var page in pages) {
        var url = "/admin/localization-preview/" + page + "?search=test&page=2";
        var initialResponse = await client.GetAsync(url);
        var initial = await initialResponse.Content.ReadAsStringAsync();
        Check(initialResponse.IsSuccessStatusCode, url + ": " + (initialResponse.IsSuccessStatusCode ? "OK" : initial));
        var token = WebUtility.HtmlDecode(Regex.Match(initial, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var change = await client.PostAsync("/language", new FormUrlEncodedContent(new Dictionary<string,string> { ["culture"] = language, ["returnUrl"] = url + "#section", ["__RequestVerificationToken"] = token }));
        Check(change.StatusCode == HttpStatusCode.Redirect && change.Headers.Location?.OriginalString == url + "#section", "language switch preserves admin path, query and fragment " + page + " " + language);
        var response = await client.GetAsync(url);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Check(response.IsSuccessStatusCode && html.Contains("<html lang=\"" + language + "\">") && response.Content.Headers.ContentLanguage.Contains(language), "renders " + page + " " + language);
        Check(html.Contains("user-language-selector") && html.Contains("value=\"vi\"") && html.Contains("value=\"en\""), "selector available " + page);
        var body = Regex.Replace(html, "<script\\b[^>]*>[\\s\\S]*?</script>|<style\\b[^>]*>[\\s\\S]*?</style>", "", RegexOptions.IgnoreCase);
        if (page != "Account/Login") Check(body.Contains(language == "en" ? "Sign out" : "Đăng xuất") || body.Contains(language == "en" ? "Log out" : "Đăng xuất"), "admin navigation localized");
        if (page == "Locations/Form") Check(body.Contains("Location name is required") == (language == "en"), "server validation localized");
        Check(body.Contains("Nội dung mẫu") || page == "Account/Login", "editorial data preserved " + page);
        if (language == "en") {
            var text = Regex.Replace(body, "<[^>]*>", " ");
            var leftovers = Regex.Matches(text, @"[^\r\n]*[À-ỹĐđ][^\r\n]*").Select(m => m.Value.Trim()).Where(s => !s.Contains("Nội dung mẫu") && !s.Contains("Tiếng Việt")).ToArray();
            Check(leftovers.Length == 0, "translated visible text " + page + ": " + string.Join(" | ", leftovers));
        }
    }
}
var missingToken = await client.PostAsync("/language", new FormUrlEncodedContent(new Dictionary<string,string> { ["culture"] = "en" }));
Check(missingToken.StatusCode == HttpStatusCode.BadRequest, "antiforgery enforced");
var markup = await client.GetStringAsync("/admin/localization-preview/Locations/Index");
var csrf = WebUtility.HtmlDecode(Regex.Match(markup,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
foreach (var target in new[] { "https://example.invalid", "//example.invalid", "/\\example.invalid" }) {
    var result = await client.PostAsync("/language", new FormUrlEncodedContent(new Dictionary<string,string> { ["culture"] = "en", ["returnUrl"] = target, ["__RequestVerificationToken"] = csrf }));
    Check(result.Headers.Location?.OriginalString == "/", "external redirect rejected " + target);
}
var unsupported = await client.PostAsync("/language", new FormUrlEncodedContent(new Dictionary<string,string> { ["culture"] = "fr", ["__RequestVerificationToken"] = csrf }));
Check(unsupported.StatusCode == HttpStatusCode.BadRequest, "unsupported culture rejected");
await app.StopAsync();

[Route("admin/localization-preview")]
public sealed class PreviewController : Controller {
    [HttpGet("{section}/{page}")]
    public IActionResult Index(string section, string page) {
        var type = (section, page) switch {
            ("Locations", "Index") => typeof(LocationIndexViewModel), ("Locations", _) => typeof(LocationFormViewModel),
            ("Branches", "Index") => typeof(BranchIndexViewModel), ("Branches", _) => typeof(BranchFormViewModel),
            ("Policies", "Index") => typeof(PolicyIndexViewModel), ("Policies", _) => typeof(PolicyFormViewModel),
            ("Tours", "Index") => typeof(TourIndexViewModel), ("Tours", "Details") => typeof(TourDetailViewModel), ("Tours", _) => typeof(TourFormViewModel),
            ("Departures", "Index") => typeof(DepartureIndexViewModel), ("Departures", "Details") => typeof(DepartureDetailViewModel), ("Departures", "Clone") => typeof(DepartureCloneViewModel), ("Departures", _) => typeof(DepartureFormViewModel),
            ("Users", "Index") => typeof(AdminUserIndexViewModel), ("Users", "Details") => typeof(AdminUserDetailsViewModel), ("Users", _) => typeof(AdminUserStatusViewModel),
            ("Bookings", "Index") => typeof(AdminBookingIndexViewModel), ("Bookings", "Details") => typeof(AdminBookingDetailsViewModel), ("Bookings", _) => typeof(AdminBookingActionViewModel),
            ("Payments", _) => typeof(AdminPaymentViewModel), _ => typeof(LoginViewModel)
        };
        var model = Sample(type);
        if (model is LoginViewModel login) login.IsAdminLogin = true;
        if (model is LocationFormViewModel) ModelState.AddModelError("Name", "Tên địa điểm không được để trống");
        TempData["Success"] = "Đã tạo tour \"Nội dung mẫu\"";
        return View("~/Views/" + (section == "Account" ? "" : "Admin/") + section + "/" + page + ".cshtml", model);
    }
    private static object Sample(Type type, int depth = 0) {
        var model = Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties().Where(p => p.CanWrite)) {
            var t = property.PropertyType;
            if (t == typeof(string)) { if (property.GetValue(model) == null || (string)property.GetValue(model)! == "") property.SetValue(model, property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase) ? "/admin/localization-preview/Locations/Index" : "Nội dung mẫu"); }
            else if (t == typeof(DateTime)) property.SetValue(model, new DateTime(2026, 11, 9, 10, 0, 0));
            else if (t == typeof(int) && property.GetValue(model) is 0) property.SetValue(model, 1);
            else if (depth < 3 && t.IsClass && t.Namespace?.StartsWith("TourismMvc.Models") == true && property.GetValue(model) == null) property.SetValue(model, Sample(t, depth + 1));
            else if (depth < 2 && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) {
                var itemType = t.GenericTypeArguments[0];
                var list = property.GetValue(model) as System.Collections.IList;
                if (list is { Count: 0 } && itemType.Namespace?.StartsWith("TourismMvc.Models") == true) list.Add(Sample(itemType, depth + 1));
            }
        }
        return model;
    }
}
