using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior",true);
void Check(bool condition,string message){if(!condition)throw new Exception("FAILED "+message);Console.WriteLine("PASS "+message);}
HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new CookieContainer()}){BaseAddress=new Uri("http://localhost:5039")};
string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
async Task Change(HttpClient client,string culture,string back="/")
{
    var page=await client.GetStringAsync("/");
    var response=await client.PostAsync("/language",new FormUrlEncodedContent(new Dictionary<string,string>{{"culture",culture},{"returnUrl",back},{"__RequestVerificationToken",Field(page,"__RequestVerificationToken")}}));
    Check(response.StatusCode==HttpStatusCode.Redirect&&response.Headers.Location!.OriginalString==back,"change locale "+culture+" preserves local return URL");
    var cookie=response.Headers.GetValues("Set-Cookie").First(s=>s.StartsWith(".TourismMvc.Language="));
    Check(cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&cookie.Contains("samesite=lax",StringComparison.OrdinalIgnoreCase)&&cookie.Contains("path=/"),"language cookie is persistent and scoped to public site");
}
using var english=Client();english.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi-VN,vi;q=0.9");
var home=await english.GetAsync("/");var html=await home.Content.ReadAsStringAsync();
Check(home.IsSuccessStatusCode&&html.Contains("<html lang=\"en\">")&&html.Contains(">Home</a>")&&html.Contains(">Sign in</a>"),"English is default even with Vietnamese browser preference");
Check(home.Content.Headers.ContentLanguage.Contains("en"),"HTML response declares English content language");
Check(html.Contains("user-language-selector")&&html.Contains("name=\"culture\" value=\"vi\""),"server-rendered language selector works without JavaScript");
var enSearch=await english.GetStringAsync("/tours?budget=under5&transportation="+Uri.EscapeDataString("Máy bay"));
var searchMarkup=Regex.Replace(enSearch,@"<script\b[^>]*>[\s\S]*?</script>","",RegexOptions.IgnoreCase);
Check(searchMarkup.Contains("Filters")&&searchMarkup.Contains("Under VND 5 million")&&searchMarkup.Contains(">Plane</label>")&&searchMarkup.Contains("value=\"Máy bay\""),"English search labels retain original transport filter values");
var absent=await english.GetAsync("/tour/NONEXISTENT-LOCALIZATION-TEST");Check(absent.StatusCode==HttpStatusCode.NotFound&&(await absent.Content.ReadAsStringAsync()).Contains("Tour not found"),"English not-found page keeps 404 status");
Check((await english.PostAsync("/language",new FormUrlEncodedContent(new Dictionary<string,string>{{"culture","vi"}}))).StatusCode==HttpStatusCode.BadRequest,"language change requires antiforgery token");
var token=Field(html,"__RequestVerificationToken");
var rejected=await english.PostAsync("/language",new FormUrlEncodedContent(new Dictionary<string,string>{{"culture","fr"},{"__RequestVerificationToken",token}}));Check(rejected.StatusCode==HttpStatusCode.BadRequest,"unsupported language rejected");
var external=await english.PostAsync("/language",new FormUrlEncodedContent(new Dictionary<string,string>{{"culture","en"},{"returnUrl","https://example.invalid"},{"__RequestVerificationToken",token}}));Check(external.StatusCode==HttpStatusCode.Redirect&&external.Headers.Location!.OriginalString=="/","external redirects rejected");
using var vietnamese=Client();await Change(vietnamese,"vi","/tours?budget=under5#results");
var viHome=await vietnamese.GetStringAsync("/");Check(viHome.Contains("<html lang=\"vi\">")&&viHome.Contains(">Trang chủ</a>")&&viHome.Contains(">Đăng nhập</a>"),"Vietnamese cookie persists across pages");
Check((await vietnamese.GetStringAsync("/tours")).Contains("Tìm Chuyến Đi Hoàn Hảo"),"Vietnamese tour search");
await Task.WhenAll(Enumerable.Range(0,8).Select(async i=>{var page=await (i%2==0?english:vietnamese).GetStringAsync("/login");Check(page.Contains(i%2==0?"<html lang=\"en\">":"<html lang=\"vi\">"),"concurrent cultures isolated "+i);}));
var adminLogin=await english.GetStringAsync("/admin/login");Check(adminLogin.Contains("<html lang=\"en\">")&&adminLogin.Contains("user-language-selector"),"admin shares English user preference and offers language selector");
var registration=await english.GetStringAsync("/register");var invalidRegistration=await english.PostAsync("/register",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Field(registration,"__RequestVerificationToken")},{"FullName","1234"},{"Email","invalid-email"},{"Password","abcdefghij"},{"ConfirmPassword","notmatching"}}));
var validation=WebUtility.HtmlDecode(await invalidRegistration.Content.ReadAsStringAsync());Check(invalidRegistration.IsSuccessStatusCode&&validation.Contains("Full name can contain only letters and spaces")&&validation.Contains("Passwords do not match"),"registration server validation translated to English");
using var fixture=JsonDocument.Parse(await File.ReadAllTextAsync(@"D:\TourismMvc\artifacts\vnpay-check\fixture.json"));
var f=fixture.RootElement;var code=f.GetProperty("code").GetString()!;var dep=f.GetProperty("departures")[8].GetInt32();
var config=new ConfigurationBuilder().SetBasePath(@"D:\TourismMvc\TourismMvc").AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",true).Build();
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(config.GetConnectionString("Default")).Options);
var initialSlots=await db.Departures.Where(d=>d.DepartureID==dep).Select(d=>d.AvailableSlots).SingleAsync();
async Task WrongLogin(HttpClient client,bool en)
{
    var page=await client.GetStringAsync("/login");var response=await client.PostAsync("/login",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Field(page,"__RequestVerificationToken")},{"Email",f.GetProperty("emails")[1].GetString()!},{"Password","NotTheFixturePassword1!"}}));
    var body=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());Check(response.IsSuccessStatusCode&&body.Contains(en?"Incorrect email or password.":"Email hoặc mật khẩu không đúng."),"authentication failure localized "+(en?"EN":"VI"));
}
await WrongLogin(english,true);await WrongLogin(vietnamese,false);
var order=await english.GetStringAsync("/order-booking?tourCode="+code+"&departureId="+dep);
Check(order.Contains("BOOK YOUR TOUR")&&order.Contains("CONTACT DETAILS")&&order.Contains(">Female</option>")&&order.Contains("value=\"Nữ\""),"English booking labels and stable gender values");
Dictionary<string,string> Values(string birth)=>new(){{"__RequestVerificationToken",Field(order,"__RequestVerificationToken")},{"Input.TourCode",code},{"Input.DepartureId",dep.ToString()},{"Input.SubmissionToken",Field(order,"Input.SubmissionToken")},{"Input.ContactFullName","Localization Fixture"},{"Input.ContactEmail","locale-booking@example.invalid"},{"Input.ContactPhone","0901234567"},{"Input.ContactAddress","Test address"},{"Input.AgreeToPolicy","true"},{"Input.Passengers[0].FullName","Test Traveller"},{"Input.Passengers[0].Gender","Nữ"},{"Input.Passengers[0].DateOfBirth",birth},{"Input.Passengers[0].PassengerType","ADULT"}};
var invalidOrder=await english.PostAsync("/order-booking",new FormUrlEncodedContent(Values("2020-01-01")));var invalidBody=WebUtility.HtmlDecode(await invalidOrder.Content.ReadAsStringAsync());
Check(invalidOrder.IsSuccessStatusCode&&invalidBody.Contains("Passenger 1's age does not match the ticket type"),"dynamic booking validation translated with passenger number");
var submitted=await english.PostAsync("/order-booking",new FormUrlEncodedContent(Values("1990-01-01")));Check(submitted.StatusCode==HttpStatusCode.Redirect,"English booking with Vietnamese gender wire value accepted");
var receipt=submitted.Headers.Location!.OriginalString;var confirmation=await english.GetStringAsync(receipt);Check(confirmation.Contains("Awaiting payment")&&confirmation.Contains("10,000")&&confirmation.Contains("Pay with VNPAY"),"English receipt and VND formatting");
await Change(english,"vi",receipt);confirmation=await english.GetStringAsync(receipt);Check(confirmation.Contains("Chờ thanh toán")&&confirmation.Contains("10.000")&&confirmation.Contains("Thanh toán qua VNPAY"),"Vietnamese receipt and prices after language switch");
var bookingCode=receipt.Split('/').Last();var booking=await db.Bookings.AsNoTracking().Include(b=>b.Passengers).SingleAsync(b=>b.BookingCode==bookingCode);
Check(booking.TotalPrice==10000&&booking.Passengers.Single().Gender=="Nữ"&&await db.Departures.Where(d=>d.DepartureID==dep).Select(d=>d.AvailableSlots).SingleAsync()==initialSlots-1,"locale switch leaves prices, stored passenger data and seat accounting valid");
async Task<string> PaymentLocale(string expected)
{
    var page=await english.GetStringAsync(receipt);var response=await english.PostAsync(receipt+"/pay",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Field(page,"__RequestVerificationToken")}}));
    Check(response.StatusCode==HttpStatusCode.Redirect&&response.Headers.Location!.Host=="sandbox.vnpayment.vn","localized payment redirects to Sandbox only");
    var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(response.Headers.Location!.Query);Check(query["vnp_Locale"]==expected,"VNPAY locale "+expected);return query["vnp_TxnRef"].ToString();
}
var viReference=await PaymentLocale("vn");await Change(english,"en",receipt);var enReference=await PaymentLocale("en");
Check(viReference==enReference&&await db.VnpayPayments.CountAsync(p=>p.BookingId==booking.BookingID)==1,"changing payment language reuses the pending transaction");
Console.WriteLine("All EN/VI localization checks passed.");
