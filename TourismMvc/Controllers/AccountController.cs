using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Security;
using TourismMvc.Services;

namespace TourismMvc.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AccountController(IAccountService accountService) : Controller
{
    [AllowAnonymous, HttpGet("/login"), HttpGet("/admin/login")]
    public IActionResult Login(string? returnUrl)
    {
        var admin = Request.Path.StartsWithSegments("/admin");
        if (User.Identity?.IsAuthenticated == true && (!admin || User.IsInRole(nameof(Role.ADMIN))))
            return RedirectAfterLogin(returnUrl, User.IsInRole(nameof(Role.ADMIN)));
        return View(new LoginViewModel { ReturnUrl = SafeReturnUrl(returnUrl), IsAdminLogin = admin });
    }

    [AllowAnonymous, HttpPost("/login"), HttpPost("/admin/login"), ValidateAntiForgeryToken, EnableRateLimiting("account")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.IsAdminLogin = Request.Path.StartsWithSegments("/admin");
        model.ReturnUrl = SafeReturnUrl(model.ReturnUrl);
        if (ModelState.IsValid)
        {
            var (user, error) = await accountService.AuthenticateAsync(model.Email, model.Password, model.IsAdminLogin);
            if (user != null)
            {
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AccountCookieEvents.CreatePrincipal(user),
                    new AuthenticationProperties { IsPersistent = model.RememberMe, ExpiresUtc = DateTimeOffset.UtcNow.Add(model.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8)), AllowRefresh = true });
                return RedirectAfterLogin(model.ReturnUrl, user.Role == Role.ADMIN);
            }
            ModelState.AddModelError("", error ?? "Không thể đăng nhập. Vui lòng thử lại.");
        }
        model.Password = "";
        return View(model);
    }

    [AllowAnonymous, HttpGet("/register")]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterViewModel());

    [AllowAnonymous, HttpPost("/register"), ValidateAntiForgeryToken, EnableRateLimiting("account")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (ModelState.IsValid)
        {
            var error = await accountService.RegisterAsync(model);
            if (error == null)
            {
                TempData["AuthSuccess"] = "Tạo tài khoản thành công. Bạn có thể đăng nhập ngay.";
                return Redirect("/login");
            }
            ModelState.AddModelError(nameof(model.Email), error);
        }
        model.Password = ""; model.ConfirmPassword = "";
        // Password inputs are rendered without value attributes, including after invalid submissions.
        return View(model);
    }

    [HttpPost("/logout"), HttpPost("/admin/logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var fromAdmin = Request.Path.StartsWithSegments("/admin");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect(fromAdmin ? "/admin/login" : "/");
    }

    [HttpGet("/access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private string? SafeReturnUrl(string? returnUrl) => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    private IActionResult RedirectAfterLogin(string? returnUrl, bool admin)
    {
        var target = SafeReturnUrl(returnUrl);
        if (target != null && (admin || !target.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)))
            return LocalRedirect(target);
        return Redirect(admin ? "/admin/tours" : "/");
    }
}
