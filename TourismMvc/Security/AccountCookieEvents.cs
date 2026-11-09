using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;

namespace TourismMvc.Security;

public class AccountCookieEvents(AppDbContext db) : CookieAuthenticationEvents
{
    private const string PasswordStampClaim = "password_stamp";
    private static string PasswordStamp(string hash) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));

    public static ClaimsPrincipal CreatePrincipal(User user) => new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture)),
        new Claim(ClaimTypes.Name, user.FullName), new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString()), new Claim(PasswordStampClaim, PasswordStamp(user.Password))
    }, CookieAuthenticationDefaults.AuthenticationScheme));

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(idClaim, out var id) ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == id) : null;
        if (context.Principal == null || user == null || !user.Status || !user.IsEmailVerified || context.Principal.FindFirstValue(PasswordStampClaim) != PasswordStamp(user.Password)
            || !context.Principal.IsInRole(user.Role.ToString()))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        if (context.Principal.Identity?.Name != user.FullName || context.Principal.FindFirstValue(ClaimTypes.Email) != user.Email)
        {
            context.ReplacePrincipal(CreatePrincipal(user)); context.ShouldRenew = true;
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        var uri = context.Request.Path.StartsWithSegments("/admin")
            ? "/admin/login" + QueryString.Create("returnUrl", context.Request.PathBase + context.Request.Path + context.Request.QueryString)
            : context.RedirectUri;
        context.Response.Redirect(uri);
        return Task.CompletedTask;
    }
}
