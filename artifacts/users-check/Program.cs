using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;
using TourismMvc.Models.Entities;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior",true);
var root=@"D:\TourismMvc";
var file=Path.Combine(root,"artifacts/users-check/fixture.json");
var config=new ConfigurationBuilder().SetBasePath(Path.Combine(root,"TourismMvc")).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",true).Build();
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(config.GetConnectionString("Default")).Options);
var mode=args.FirstOrDefault()??"test";
if(mode=="seed")
{
    if(File.Exists(file))throw new Exception("Clean existing fixture first");
    var marker=Guid.NewGuid().ToString("N")[..12];var password="Aa1!"+Guid.NewGuid().ToString("N");
    var users=Enumerable.Range(0,10).Select(i=>new User{Email=$"users-check-{marker}-{i}@example.invalid",FullName=$"Users Fixture {marker} {i}",Phone="0901234567",Password=BCrypt.Net.BCrypt.HashPassword(password),Role=i<2?Role.ADMIN:Role.CUSTOMER,Status=i!=9,IsEmailVerified=true}).ToArray();
    users[8].FullName="<script>alert('fixture')</script>";
    db.Users.AddRange(users);await db.SaveChangesAsync();
    var dep=await db.Departures.OrderBy(d=>d.DepartureID).Select(d=>d.DepartureID).FirstAsync();
    var bookings=Enumerable.Range(0,12).Select(i=>new Booking{BookingCode="US"+marker+i.ToString("D2"),UserId=users[2].UserID,DepartureId=dep,ContactEmail=users[2].Email,ContactFullName=users[2].FullName,ContactPhone="0901234567",ContactAddress="Test fixture",TotalPassengers=1,SubtotalPrice=1234567,TotalPrice=1234567,BookingStatus=i%3==0?BookingStatus.PAID:i%3==1?BookingStatus.CANCELLED:BookingStatus.PENDING_CONFIRMATION}).ToArray();
    db.Bookings.AddRange(bookings);await db.SaveChangesAsync();
    await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{marker,password,ids=users.Select(u=>u.UserID),emails=users.Select(u=>u.Email)}));
    Console.WriteLine("Temporary users and booking history prepared.");return;
}
using var json=JsonDocument.Parse(await File.ReadAllTextAsync(file));var fixture=json.RootElement;
var markerValue=fixture.GetProperty("marker").GetString()!;var ids=fixture.GetProperty("ids").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var emails=fixture.GetProperty("emails").EnumerateArray().Select(x=>x.GetString()!).ToArray();var pass=fixture.GetProperty("password").GetString()!;
if(mode=="cleanup")
{
    if(emails.Length!=10||emails.Any(e=>!e.StartsWith("users-check-"+markerValue+"-",StringComparison.Ordinal)||!e.EndsWith("@example.invalid",StringComparison.Ordinal)))throw new Exception("Unsafe fixture scope");
    await using var tx=await db.Database.BeginTransactionAsync();
    await db.Bookings.Where(b=>ids.Contains(b.UserId??-1)&&b.BookingCode.StartsWith("US"+markerValue)).ExecuteDeleteAsync();
    await db.Users.Where(u=>ids.Contains(u.UserID)&&emails.Contains(u.Email)).ExecuteDeleteAsync();
    await tx.CommitAsync();File.Delete(file);Console.WriteLine("Temporary accounts and history removed.");return;
}
void Check(bool condition,string label){if(!condition)throw new Exception("FAILED "+label);Console.WriteLine("PASS "+label);}
HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new CookieContainer()}){BaseAddress=new Uri("http://localhost:5036")};
string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
async Task Login(HttpClient c,int index,bool admin=true)
{
    var path=admin?"/admin/login":"/login";var page=await c.GetStringAsync(path);
    var result=await c.PostAsync(path,new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",emails[index]},{"Password",pass},{"__RequestVerificationToken",Token(page)}}));
    Check(result.StatusCode==HttpStatusCode.Redirect,"login fixture "+index);
}
async Task<HttpResponseMessage> Status(HttpClient c,int index,string? status,string? back=null,bool csrf=true)
{
    var page=await c.GetStringAsync("/admin/users/"+ids[index]+"/status");
    var values=new Dictionary<string,string>{{"ReturnUrl",back??"/admin/users?email="+emails[index]}};
    if(status!=null)values["Status"]=status;if(csrf)values["__RequestVerificationToken"]=Token(page);
    return await c.PostAsync("/admin/users/"+ids[index]+"/status",new FormUrlEncodedContent(values));
}
using var anonymous=Client();var challenge=await anonymous.GetAsync("/admin/users");Check(challenge.StatusCode==HttpStatusCode.Redirect&&challenge.Headers.Location!.OriginalString.StartsWith("/admin/login"),"anonymous redirected to admin login");
using var adminA=Client();using var adminB=Client();using var customer=Client();await Login(adminA,0);await Login(adminB,1);await Login(customer,2,false);
Check((await customer.GetAsync("/admin/users")).StatusCode==HttpStatusCode.Redirect,"customer cannot view admin users");
Check((await customer.PostAsync("/admin/users/"+ids[3]+"/status",new FormUrlEncodedContent(new Dictionary<string,string>{{"Status","false"}}))).StatusCode==HttpStatusCode.Redirect,"customer cannot mutate account status");
var result=await adminA.GetAsync("/admin/users?email=users-check-"+markerValue+"&pageSize=6");var html=await result.Content.ReadAsStringAsync();
Check(result.IsSuccessStatusCode&&Regex.Matches(html,"data-user-id=").Count==6&&html.Contains("rel=\"next\""),"list paginates matching accounts");
Check(!html.Contains("$2a$")&&!html.Contains("$2b$")&&!html.Contains(pass),"no password or password hash in page");
Check(html.Contains("&lt;script&gt;")&&!html.Contains("<script>alert('fixture')"),"user names HTML encoded");
var filtered=await adminA.GetStringAsync("/admin/users?email="+emails[2]+"&fullName="+Uri.EscapeDataString("USERS FIXTURE")+"&phone=090&status=active&role=CUSTOMER");
Check(Regex.Matches(filtered,"data-user-id=").Count==1&&filtered.Contains("12 đơn đặt tour"),"combined name, phone, email, status and role filters");
var locked=await adminA.GetStringAsync("/admin/users?email=users-check-"+markerValue+"&status=locked");Check(Regex.Matches(locked,"data-user-id=").Count==1,"locked filter");
Check((await adminA.GetStringAsync("/admin/users?email=users-check-"+markerValue+"&page=2147483647&pageSize=-1")).Contains("data-user-id="),"oversized page and invalid page size safely normalized");
Check((await adminA.GetStringAsync("/admin/users?email=%25%27%20OR%201%3D1--")).Contains("Không tìm thấy người dùng phù hợp"),"literal search input handled safely");
var detail=await adminA.GetStringAsync("/admin/users/"+ids[2]);Check(detail.Contains("Chờ xác nhận")&&detail.Contains("Đã thanh toán")&&detail.Contains("Đã hủy")&&Regex.Matches(detail,"<tr>").Count==11,"account booking history first page");
Check(Regex.Matches(await adminA.GetStringAsync("/admin/users/"+ids[2]+"?page=2"),"<tr>").Count==3,"account booking history second page");
var before=await db.Users.AsNoTracking().Where(u=>u.UserID==ids[2]).Select(u=>u.Status).SingleAsync();await adminA.GetAsync("/admin/users/"+ids[2]+"/status");Check(before&&await db.Users.AsNoTracking().Where(u=>u.UserID==ids[2]).Select(u=>u.Status).SingleAsync(),"confirmation GET does not change status");
Check((await Status(adminA,2,"false",csrf:false)).StatusCode==HttpStatusCode.BadRequest,"status change requires antiforgery token");
await Status(adminA,2,null);Check(await db.Users.AsNoTracking().Where(u=>u.UserID==ids[2]).Select(u=>u.Status).SingleAsync(),"missing desired status rejected");
await Status(adminA,0,"false");Check(await db.Users.AsNoTracking().Where(u=>u.UserID==ids[0]).Select(u=>u.Status).SingleAsync(),"self lock rejected server side");
var lockResponse=await Status(adminA,2,"false","https://example.invalid");Check(lockResponse.Headers.Location!.OriginalString=="/admin/users"&&!await db.Users.AsNoTracking().Where(u=>u.UserID==ids[2]).Select(u=>u.Status).SingleAsync(),"lock persists and external redirect rejected");
var revoked=await customer.GetStringAsync("/");Check(!revoked.Contains(emails[2]),"locked user existing session revoked");
using var blocked=Client();var loginPage=await blocked.GetStringAsync("/login");var denied=await blocked.PostAsync("/login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",emails[2]},{"Password",pass},{"__RequestVerificationToken",Token(loginPage)}}));Check(denied.StatusCode==HttpStatusCode.OK,"locked user cannot log in");
await Status(adminA,2,"true");Check(await db.Users.AsNoTracking().Where(u=>u.UserID==ids[2]).Select(u=>u.Status).SingleAsync(),"unlock persists");await Login(customer,2,false);
Check(await db.Bookings.CountAsync(b=>b.UserId==ids[2])==12,"locking preserves bookings");
var aToken=Token(await adminA.GetStringAsync("/admin/users/"+ids[1]+"/status"));var bToken=Token(await adminB.GetStringAsync("/admin/users/"+ids[0]+"/status"));
await Task.WhenAll(adminA.PostAsync("/admin/users/"+ids[1]+"/status",new FormUrlEncodedContent(new Dictionary<string,string>{{"Status","false"},{"__RequestVerificationToken",aToken}})),adminB.PostAsync("/admin/users/"+ids[0]+"/status",new FormUrlEncodedContent(new Dictionary<string,string>{{"Status","false"},{"__RequestVerificationToken",bToken}})));
Check(await db.Users.CountAsync(u=>(u.UserID==ids[0]||u.UserID==ids[1])&&u.Status)==1,"concurrent administrators cannot lock each other out");
await db.Users.Where(u=>ids.Contains(u.UserID)&&u.UserID!=ids[9]).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Status,true));
Console.WriteLine("All admin user integration checks passed. Fixture retained for browser checks.");
