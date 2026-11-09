using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TourismMvc.Security;
using TourismMvc.Payments;
using TourismMvc.Localization;
using Microsoft.AspNetCore.Localization;

// Cột thời gian trong DB là "timestamp without time zone" (do Hibernate tạo) -> dùng hành vi cũ của Npgsql
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Vnpay.json", optional: false)
    .AddJsonFile($"appsettings.Vnpay.{builder.Environment.EnvironmentName}.local.json", optional: true)
    .AddEnvironmentVariables().AddCommandLine(args);
builder.Services.Configure<VnpayOptions>(builder.Configuration.GetSection("Vnpay"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<VnpayQueryClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    // The Sandbox API rejects requests without an identifying User-Agent with HTTP 403.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TourismMvc/1.0");
});
builder.Services.AddScoped<IVnpayPaymentService, VnpayPaymentService>();
builder.Services.AddHostedService<VnpayReconciliationWorker>();

// Add services to the container.
builder.Services.AddSingleton<UserTranslationCatalog>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserText>();
builder.Services.AddControllersWithViews(options => options.Filters.Add<UserLocalizationFilter>());
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = ".TourismMvc.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.EventsType = typeof(AccountCookieEvents);
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        (context.Connection.RemoteIpAddress?.ToString() ?? "unknown") + context.Request.Path.ToString().ToLowerInvariant(),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync(context.HttpContext.RequestServices.GetRequiredService<UserText>()["Bạn thao tác quá nhiều lần. Vui lòng thử lại sau một phút."], token);
    };
});
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Đăng ký Application Services
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IBranchContactService, BranchContactService>();
builder.Services.AddScoped<IPolicyTemplateService, PolicyTemplateService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IDepartureService, DepartureService>();
builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IPublicTourSearchService, PublicTourSearchService>();
builder.Services.AddScoped<IPublicTourDetailService, PublicTourDetailService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminBookingService, AdminBookingService>();
builder.Services.AddScoped<IBookingOrderService, BookingOrderService>();
builder.Services.AddSingleton<BookingSubmissionProtector>();

// Razor không mã hoá tiếng Việt thành &#x...;
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();   // phục vụ ảnh upload runtime trong wwwroot/uploads
app.UseRouting();
app.UseWhen(UserText.IsUserRequest, localized => localized.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = new[] { System.Globalization.CultureInfo.GetCultureInfo("en"), System.Globalization.CultureInfo.GetCultureInfo("vi") },
    SupportedUICultures = new[] { System.Globalization.CultureInfo.GetCultureInfo("en"), System.Globalization.CultureInfo.GetCultureInfo("vi") },
    RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider { CookieName = ".TourismMvc.Language" } }
}));

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
