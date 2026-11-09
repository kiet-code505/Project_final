using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;
using TourismMvc.Models.Entities;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var root = @"D:\TourismMvc";
var fixturePath = Path.Combine(root, "artifacts/auth-check/java-password.json");
var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
var password = fixture.RootElement.GetProperty("password").GetString()!;
var javaHash = fixture.RootElement.GetProperty("hash").GetString()!;
var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(root, "TourismMvc")).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional:true).Build();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(configuration.GetConnectionString("Default")).Options);
var marker = Guid.NewGuid().ToString("N");
var registeredEmail = $"auth-check-customer-{marker}@example.invalid";
var adminEmail = $"auth-check-admin-{marker}@example.invalid";
var brokenEmail = $"auth-check-invalid-{marker}@example.invalid";
var emails = new[] { registeredEmail, adminEmail, brokenEmail };
var baseUri = new Uri("http://localhost:5032");
HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = baseUri };
void Check(bool valid, string message) { if (!valid) throw new Exception("FAILED: " + message); Console.WriteLine("PASS " + message); }
async Task<(string Html, string Token)> Form(HttpClient client, string path)
{
    var response = await client.GetAsync(path); var html = await response.Content.ReadAsStringAsync();
    Check(response.StatusCode == HttpStatusCode.OK, "GET " + path.Split('?')[0]);
    var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    if (token.Length == 0) throw new Exception("Antiforgery token missing");
    return (html, WebUtility.HtmlDecode(token));
}
Task<HttpResponseMessage> Post(HttpClient client, string path, string token, Dictionary<string,string> values)
{
    values["__RequestVerificationToken"] = token;
    return client.PostAsync(path, new FormUrlEncodedContent(values));
}
Dictionary<string,string> LoginValues(string email) => new() { ["Email"]=email, ["Password"]=password };
try
{
    Check(BCrypt.Net.BCrypt.Verify(password, javaHash), "Java Spring BCrypt compatibility");
    var admin = new User { Email=adminEmail, FullName="Auth Check Admin", Password=javaHash, Role=Role.ADMIN, Status=true, IsEmailVerified=true };
    var broken = new User { Email=brokenEmail, FullName="Auth Check Invalid", Password="invalid-hash", Role=Role.CUSTOMER, Status=true, IsEmailVerified=true };
    db.Users.AddRange(admin, broken); await db.SaveChangesAsync();
    using var guest = Client(); using var customer = Client(); using var manager = Client();
    foreach (var path in new[] { "/admin", "/admin/dashboard", "/admin/tours", "/admin/tours/check-code?tourCode=TEST", "/admin/locations", "/admin/branches", "/admin/policies", "/admin/departures" })
    {
        var denied = await guest.GetAsync(path);
        Check(denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location!.OriginalString.Contains("/admin/login"), "anonymous blocked " + path.Split('?')[0]);
    }
    var registerForm = await Form(guest,"/register");
    var values = new Dictionary<string,string> { ["FullName"]="Auth Check Customer", ["Email"]=registeredEmail, ["Phone"]="0901234567", ["Password"]=password, ["ConfirmPassword"]=password, ["Role"]="ADMIN" };
    var register = await Post(guest,"/register",registerForm.Token,values);
    Check(register.StatusCode == HttpStatusCode.Redirect && register.Headers.Location!.OriginalString == "/login", "customer registration");
    var saved = await db.Users.AsNoTracking().SingleAsync(u=>u.Email==registeredEmail);
    Check(saved.Role == Role.CUSTOMER && saved.Status && saved.IsEmailVerified && saved.Password != password && BCrypt.Net.BCrypt.Verify(password,saved.Password), "registration cannot assign ADMIN and stores BCrypt hash");
    var successPage=await Form(guest,"/login");
    Check(successPage.Html.Contains("Tạo tài khoản thành công"), "registration success message");
    var duplicateForm=await Form(guest,"/register"); values["Email"]=registeredEmail.ToUpperInvariant();
    var duplicate=await Post(guest,"/register",duplicateForm.Token,values); var duplicateHtml=await duplicate.Content.ReadAsStringAsync();
    Check(duplicate.StatusCode==HttpStatusCode.OK && duplicateHtml.Contains("Email đã được đăng ký") && !duplicateHtml.Contains(password), "duplicate email case insensitive and password not echoed");
    values["Email"]=registeredEmail; values["Password"]="Aa1!"+new string('é',40); values["ConfirmPassword"]=values["Password"];
    var invalid=await Post(guest,"/register",duplicateForm.Token,values);
    Check(invalid.StatusCode==HttpStatusCode.OK && (await invalid.Content.ReadAsStringAsync()).Contains("72 byte"), "BCrypt UTF-8 password length validation");
    var customerForm=await Form(customer,"/login?returnUrl=https%3A%2F%2Fexample.invalid%2Fescape");
    var wrong=LoginValues(registeredEmail);wrong["Password"]="IncorrectPassword1";
    var rejected=await Post(customer,"/login",customerForm.Token,wrong);
    Check((await rejected.Content.ReadAsStringAsync()).Contains("Email hoặc mật khẩu không đúng"), "wrong credentials rejected");
    var malformed=await Post(customer,"/login",customerForm.Token,LoginValues(brokenEmail));
    Check(malformed.StatusCode==HttpStatusCode.OK && (await malformed.Content.ReadAsStringAsync()).Contains("Email hoặc mật khẩu không đúng"), "invalid stored hash handled");
    var loginValues=LoginValues(registeredEmail.ToUpperInvariant());loginValues["ReturnUrl"]="https://example.invalid/escape";
    var login=await Post(customer,"/login",customerForm.Token,loginValues);
    Check(login.StatusCode==HttpStatusCode.Redirect && login.Headers.Location!.OriginalString=="/", "customer login and external redirect blocked");
    var cookie=login.Headers.GetValues("Set-Cookie").First(c=>c.StartsWith(".TourismMvc.Auth="));
    Check(cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase) && cookie.Contains("samesite=lax",StringComparison.OrdinalIgnoreCase) && !cookie.Contains(password), "protected HttpOnly SameSite auth cookie");
    var customerHome=await Form(customer,"/");Check(customerHome.Html.Contains("Auth Check Customer") && customerHome.Html.Contains("action=\"/logout\""), "signed in user header");
    var blocked=await customer.GetAsync("/admin/tours");
    Check(blocked.StatusCode==HttpStatusCode.Redirect && blocked.Headers.Location!.OriginalString.Contains("/access-denied"), "customer blocked from admin");
    Check((await customer.GetAsync("/access-denied")).StatusCode==HttpStatusCode.Forbidden, "friendly 403 page");
    var adminFormForCustomer=await Form(customer,"/admin/login");
    var noAdmin=await Post(customer,"/admin/login",adminFormForCustomer.Token,LoginValues(registeredEmail));
    Check(noAdmin.StatusCode==HttpStatusCode.OK && (await noAdmin.Content.ReadAsStringAsync()).Contains("không có quyền quản trị"), "customer cannot sign in as admin");
    var csrfDenied=await customer.PostAsync("/logout",new FormUrlEncodedContent(new Dictionary<string,string>()));
    Check(csrfDenied.StatusCode==HttpStatusCode.BadRequest, "logout requires antiforgery token");
    Check((await customer.GetAsync("/logout")).StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, "GET cannot perform logout");
    var refreshedHome=await Form(customer,"/");
    var logout=await Post(customer,"/logout",refreshedHome.Token,new());
    Check(logout.StatusCode==HttpStatusCode.Redirect && logout.Headers.Location!.OriginalString=="/", "customer logout");
    Check(!(await (await customer.GetAsync("/")).Content.ReadAsStringAsync()).Contains("class=\"account-menu\""), "cookie cleared after logout");
    var adminForm=await Form(manager,"/admin/login");var adminValues=LoginValues(adminEmail);adminValues["ReturnUrl"]="/admin/departures";adminValues["RememberMe"]="true";
    var adminLogin=await Post(manager,"/admin/login",adminForm.Token,adminValues);
    Check(adminLogin.StatusCode==HttpStatusCode.Redirect && adminLogin.Headers.Location!.OriginalString=="/admin/departures", "admin login returns to protected page");
    Check(adminLogin.Headers.GetValues("Set-Cookie").Any(c=>c.StartsWith(".TourismMvc.Auth=")&&c.Contains("expires=",StringComparison.OrdinalIgnoreCase)), "remember me persistent cookie");
    foreach(var path in new[]{"/admin/tours","/admin/locations","/admin/branches","/admin/policies","/admin/departures"}) Check((await manager.GetAsync(path)).StatusCode==HttpStatusCode.OK,"admin authorized "+path);
    Check((await manager.PostAsync("/admin/tours/2147483647/delete",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"admin writes still require antiforgery");
    admin.Role=Role.CUSTOMER;await db.SaveChangesAsync();
    var revoked=await manager.GetAsync("/admin/tours");
    Check(revoked.StatusCode==HttpStatusCode.Redirect && revoked.Headers.Location!.OriginalString.Contains("/admin/login"),"role change revokes existing session");
    admin.Role=Role.ADMIN;admin.Status=false;await db.SaveChangesAsync();
    var lockedForm=await Form(manager,"/admin/login");var locked=await Post(manager,"/admin/login",lockedForm.Token,LoginValues(adminEmail));
    Check(locked.StatusCode==HttpStatusCode.OK && (await locked.Content.ReadAsStringAsync()).Contains("Tài khoản đã bị khóa"),"locked account rejected");
    admin.Status=true;await db.SaveChangesAsync();
    using var generalAdmin=Client();var generalAdminForm=await Form(generalAdmin,"/login");
    var generalAdminLogin=await Post(generalAdmin,"/login",generalAdminForm.Token,LoginValues(adminEmail));
    Check(generalAdminLogin.StatusCode==HttpStatusCode.Redirect && generalAdminLogin.Headers.Location!.OriginalString=="/admin/tours","admin can use general login and reaches admin area");
    using var passwordClient=Client();var passwordForm=await Form(passwordClient,"/login");
    var passwordLogin=await Post(passwordClient,"/login",passwordForm.Token,LoginValues(registeredEmail));
    Check(passwordLogin.StatusCode==HttpStatusCode.Redirect,"session before password change");
    var changedUser=await db.Users.SingleAsync(u=>u.Email==registeredEmail);var oldHash=changedUser.Password;changedUser.Password=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));await db.SaveChangesAsync();
    Check(!(await (await passwordClient.GetAsync("/")).Content.ReadAsStringAsync()).Contains("class=\"account-menu\""),"password change revokes session");
    changedUser.Password=oldHash;await db.SaveChangesAsync();
    var unverified=await db.Users.SingleAsync(u=>u.Email==registeredEmail);unverified.IsEmailVerified=false;await db.SaveChangesAsync();
    using var unverifiedClient=Client();var verificationForm=await Form(unverifiedClient,"/login");
    var verificationLogin=await Post(unverifiedClient,"/login",verificationForm.Token,LoginValues(registeredEmail));
    Check(verificationLogin.StatusCode==HttpStatusCode.OK && (await verificationLogin.Content.ReadAsStringAsync()).Contains("chưa được xác thực"),"unverified existing account rejected");
    var limited=false;for(var i=0;i<22;i++){var response=await Post(guest,"/login",successPage.Token,new(){["Email"]="",["Password"]=""});if((int)response.StatusCode==429){limited=true;break;}}
    Check(limited,"login request rate limit");
    Console.WriteLine("ALL AUTHENTICATION CHECKS PASSED");
}
finally
{
    await db.Users.Where(u=>emails.Contains(u.Email)).ExecuteDeleteAsync();
    if(File.Exists(fixturePath)) File.Delete(fixturePath);
    Console.WriteLine("Temporary authentication accounts and password fixture removed.");
}
