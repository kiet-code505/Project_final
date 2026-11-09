using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace TourismMvc.Controllers;

[AllowAnonymous]
public sealed class LanguageController : Controller
{
    [HttpPost("/language"), ValidateAntiForgeryToken]
    public IActionResult Change(string culture, string? returnUrl)
    {
        if (culture is not ("en" or "vi")) return BadRequest();
        Response.Cookies.Append(".TourismMvc.Language", CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)), new CookieOptions
        {
            HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps,
            Path = "/", MaxAge = TimeSpan.FromDays(365)
        });
        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? returnUrl : "/");
    }
}
