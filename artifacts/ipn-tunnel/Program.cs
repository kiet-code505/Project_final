var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddHttpClient("mvc", client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:5019");
    client.Timeout = TimeSpan.FromSeconds(20);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
var app = builder.Build();
app.MapGet("/payment/vnpay/ipn", async (HttpContext context, IHttpClientFactory clients) =>
{
    if ((context.Request.QueryString.Value?.Length ?? 0) > 8000) return Results.BadRequest();
    try
    {
        using var response = await clients.CreateClient("mvc").GetAsync("/payment/vnpay/ipn" + context.Request.QueryString, context.RequestAborted);
        if (!response.IsSuccessStatusCode) return Results.Json(new { RspCode = "99", Message = "Please retry" });
        var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
        return Results.Content(body, "application/json; charset=utf-8");
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
    {
        return Results.Json(new { RspCode = "99", Message = "Please retry" });
    }
});
app.MapFallback(() => Results.NotFound());
app.Run("http://127.0.0.1:5187");
