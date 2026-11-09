using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;
using TourismMvc.Models.Entities;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior",true);
var root=@"D:\TourismMvc";
var fixturePath=Path.Combine(root,"artifacts/booking-check/fixture.json");
var config=new ConfigurationBuilder().SetBasePath(Path.Combine(root,"TourismMvc")).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",true).Build();
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(config.GetConnectionString("Default")).Options);
var mode=args.FirstOrDefault()??"test";
if(mode is "schema" or "migrate")
{
    var definition=await db.Database.SqlQueryRaw<string>("SELECT pg_get_constraintdef(oid) AS \"Value\" FROM pg_constraint WHERE conrelid = 'public.tour_departures'::regclass AND conname = 'tour_departures_available_slots_check'").SingleOrDefaultAsync();
    Console.WriteLine("Available slots constraint: "+definition);
    if(mode=="migrate")
    {
        if(definition is not "CHECK ((available_slots >= 1))" and not "CHECK ((available_slots >= 0))")throw new Exception("Unexpected available slots constraint; refusing schema change.");
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root,"TourismMvc/Data/booking_allow_zero_slots.sql")));
        Console.WriteLine("Applied nonnegative remaining seats constraint.");
    }
    return;
}
if(mode=="seed")
{
    if(File.Exists(fixturePath))throw new Exception("Existing fixture must be cleaned first");
    var marker=Guid.NewGuid().ToString("N");var password="Aa1!"+Guid.NewGuid().ToString("N");
    var locations=await db.Locations.OrderBy(l=>l.LocationID).Select(l=>l.LocationID).Take(2).ToListAsync();var policy=await db.Policies.OrderBy(p=>p.PolicyTemplateID).Select(p=>p.PolicyTemplateID).FirstAsync();
    var users=new[]{new User{Email=$"booking-check-a-{marker}@example.invalid",FullName="Booking Check User",Phone="0901234567",Password=BCrypt.Net.BCrypt.HashPassword(password),Role=Role.CUSTOMER,Status=true,IsEmailVerified=true},new User{Email=$"booking-check-b-{marker}@example.invalid",FullName="Booking Check Other",Password=BCrypt.Net.BCrypt.HashPassword(password),Role=Role.CUSTOMER,Status=true,IsEmailVerified=true}};
    var fixtureCode="BOOK-CHECK-"+marker[..16].ToUpperInvariant();
    var tour=new Tour{TourCode=fixtureCode,TourName="Tour kiểm thử đặt chỗ tự động",Duration="3 ngày 2 đêm",Transportation="Xe khách",Attractions="Dữ liệu kiểm thử tạm",StartLocationId=locations[0],EndLocationId=locations[^1],Status=true};
    var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"SE Asia Standard Time").Date;
    var names=new[]{"main","race","owner","validation","past","noAdult","quote","duplicate","ui"};
    var departures=names.Select((name,i)=>new TourDeparture{Tour=tour,PolicyTemplateId=policy,DepartureDate=name=="past"?today.AddDays(-1):today.AddMonths(2).AddDays(i),AvailableSlots=name=="main"?3:name=="race"?1:5,Status=true,TourGuideInfo="Hướng dẫn viên kiểm thử"}).ToArray();
    foreach(var dep in departures)
    {
        if(dep!=departures[5])dep.Pricings.Add(new DeparturePricing{PassengerType=PassengerType.ADULT,AgeDescription="Từ 12 tuổi",OriginalPrice=5000000});
        dep.Pricings.Add(new DeparturePricing{PassengerType=PassengerType.CHILD,AgeDescription="Từ 2 đến 11 tuổi",OriginalPrice=2500000});
        dep.Pricings.Add(new DeparturePricing{PassengerType=PassengerType.INFANT,AgeDescription="Dưới 2 tuổi",OriginalPrice=500000});
    }
    db.Users.AddRange(users);db.Departures.AddRange(departures);await db.SaveChangesAsync();
    await File.WriteAllTextAsync(fixturePath,JsonSerializer.Serialize(new{code=fixtureCode,password,emails=users.Select(u=>u.Email),userIds=users.Select(u=>u.UserID),departures=names.Select((name,i)=>new{name,id=departures[i].DepartureID,date=departures[i].DepartureDate.ToString("yyyy-MM-dd")})}));
    Console.WriteLine("Booking fixture prepared.");return;
}
using var data=JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));var f=data.RootElement;
var tourCode=f.GetProperty("code").GetString()!;var emails=f.GetProperty("emails").EnumerateArray().Select(x=>x.GetString()!).ToArray();
if(!tourCode.StartsWith("BOOK-CHECK-")||emails.Any(e=>!e.StartsWith("booking-check-")||!e.EndsWith("@example.invalid")))throw new Exception("Invalid cleanup scope");
var depMap=f.GetProperty("departures").EnumerateArray().ToDictionary(x=>x.GetProperty("name").GetString()!,x=>x.GetProperty("id").GetInt32());
if(mode=="cleanup")
{
    await using var tx=await db.Database.BeginTransactionAsync();var tourId=await db.Tours.Where(t=>t.TourCode==tourCode).Select(t=>t.TourID).SingleAsync();
    var depIds=await db.Departures.Where(d=>d.TourId==tourId).Select(d=>d.DepartureID).ToListAsync();var bookingIds=await db.Bookings.Where(b=>depIds.Contains(b.DepartureId)).Select(b=>b.BookingID).ToListAsync();
    await db.BookingPassengers.Where(p=>bookingIds.Contains(p.BookingId)).ExecuteDeleteAsync();await db.Bookings.Where(b=>bookingIds.Contains(b.BookingID)).ExecuteDeleteAsync();
    await db.Pricings.Where(p=>depIds.Contains(p.DepartureId)).ExecuteDeleteAsync();await db.Transports.Where(p=>depIds.Contains(p.DepartureId)).ExecuteDeleteAsync();await db.Departures.Where(d=>depIds.Contains(d.DepartureID)).ExecuteDeleteAsync();
    await db.Tours.Where(t=>t.TourID==tourId).ExecuteDeleteAsync();await db.Users.Where(u=>emails.Contains(u.Email)).ExecuteDeleteAsync();await tx.CommitAsync();File.Delete(fixturePath);Console.WriteLine("Booking fixture, users and test reservations removed.");return;
}
HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new CookieContainer()}){BaseAddress=new Uri("http://localhost:5035")};
void Check(bool ok,string message){if(!ok)throw new Exception("FAILED: "+message);Console.WriteLine("PASS "+message);}
string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
async Task<(string Html,string Csrf,string Token)> Order(HttpClient client,string name)
{
    var response=await client.GetAsync("/order-booking?tourCode="+tourCode+"&departureId="+depMap[name]);var html=await response.Content.ReadAsStringAsync();
    Check(response.StatusCode==HttpStatusCode.OK,"GET booking "+name);return(html,Field(html,"__RequestVerificationToken"),Field(html,"Input.SubmissionToken"));
}
Dictionary<string,string> Values(string name,string csrf,string token,params string[] types)
{
    var date=DateOnly.Parse(f.GetProperty("departures").EnumerateArray().First(x=>x.GetProperty("name").GetString()==name).GetProperty("date").GetString()!);
    var values=new Dictionary<string,string>{{"__RequestVerificationToken",csrf},{"Input.SubmissionToken",token},{"Input.TourCode",tourCode},{"Input.DepartureId",depMap[name].ToString()},{"Input.ContactFullName","Booking Test Contact"},{"Input.ContactEmail","booking-test@example.invalid"},{"Input.ContactPhone","0901234567"},{"Input.ContactAddress","Địa chỉ kiểm thử tạm"},{"Input.AgreeToPolicy","true"},{"Input.TotalPrice","1"},{"Input.UserId","1"}};
    for(var i=0;i<types.Length;i++){var p="Input.Passengers["+i+"].";values[p+"PassengerType"]=types[i];values[p+"FullName"]="Test Passenger";values[p+"Gender"]="Nam";values[p+"DateOfBirth"]=date.AddYears(types[i]=="ADULT"?-30:types[i]=="CHILD"?-7:-1).ToString("yyyy-MM-dd");values[p+"BasePrice"]="1";}
    return values;
}
Task<HttpResponseMessage> Post(HttpClient c,Dictionary<string,string> values)=>c.PostAsync("/order-booking",new FormUrlEncodedContent(values));
string Code(HttpResponseMessage response)=>response.Headers.Location!.OriginalString.Split('/').Last();
using var guest=Client();var main=await Order(guest,"main");
Check(!string.IsNullOrEmpty(main.Token)&&!string.IsNullOrEmpty(main.Csrf),"protected submission and antiforgery tokens");
var missingCsrf=await guest.PostAsync("/order-booking",new FormUrlEncodedContent(new Dictionary<string,string>()));Check(missingCsrf.StatusCode==HttpStatusCode.BadRequest,"booking requires CSRF token");
var mainValues=Values("main",main.Csrf,main.Token,"ADULT","CHILD","INFANT");var created=await Post(guest,mainValues);
Check(created.StatusCode==HttpStatusCode.Redirect,"guest booking submitted");var code=Code(created);
var saved=await db.Bookings.AsNoTracking().Include(b=>b.Passengers).SingleAsync(b=>b.BookingCode==code);
Check(saved.UserId==null&&saved.BookingStatus==BookingStatus.PENDING_CONFIRMATION&&saved.TotalPrice==8000000&&saved.SubtotalPrice==8000000&&saved.Passengers.Count==3,"server prices, guest ownership and pending confirmation");
Check(await db.Departures.Where(d=>d.DepartureID==depMap["main"]).Select(d=>d.AvailableSlots).SingleAsync()==1,"infant does not consume a seat");
var retry=await Post(guest,mainValues);Check(retry.StatusCode==HttpStatusCode.Redirect&&Code(retry)==code,"retry returns same booking");
Check(await db.Bookings.CountAsync(b=>b.DepartureId==depMap["main"])==1&&await db.Departures.Where(d=>d.DepartureID==depMap["main"]).Select(d=>d.AvailableSlots).SingleAsync()==1,"retry does not consume seats twice");
Check((await guest.GetAsync(created.Headers.Location)).StatusCode==HttpStatusCode.OK,"guest private receipt");using var outsider=Client();Check((await outsider.GetAsync(created.Headers.Location)).StatusCode==HttpStatusCode.NotFound,"other browser cannot read guest receipt");
var validation=await Order(guest,"validation");var bad=Values("validation",validation.Csrf,validation.Token,"ADULT");bad["Input.Passengers[0].DateOfBirth"]=DateOnly.FromDateTime(DateTime.Today).AddYears(-5).ToString("yyyy-MM-dd");
var age=await Post(guest,bad);Check(age.StatusCode==HttpStatusCode.OK&&(await age.Content.ReadAsStringAsync()).Contains("Độ tuổi hành khách"),"server rejects wrong passenger age");
bad=Values("validation",validation.Csrf,validation.Token,"ADULT");bad["Input.AgreeToPolicy"]="false";Check((await Post(guest,bad)).StatusCode==HttpStatusCode.OK,"policy agreement required");
bad=Values("validation",validation.Csrf,validation.Token,"ADULT");bad["Input.SubmissionToken"]="tampered-token";var tampered=await Post(guest,bad);Check(tampered.StatusCode==HttpStatusCode.OK&&(await tampered.Content.ReadAsStringAsync()).Contains("Phiên đặt tour"),"tampered draft token rejected");
Check(await db.Bookings.CountAsync(b=>b.DepartureId==depMap["validation"])==0&&await db.Departures.Where(d=>d.DepartureID==depMap["validation"]).Select(d=>d.AvailableSlots).SingleAsync()==5,"validation failures do not reserve seats");
var past=await Order(guest,"past");Check(past.Html.Contains("ngừng nhận đặt")&&past.Html.Contains("id=\"submit-booking\" disabled"),"past departure cannot be booked");
var noAdult=await Order(guest,"noAdult");Check(noAdult.Html.Contains("id=\"submit-booking\" disabled"),"missing adult price disables booking");
var quote=await Order(guest,"quote");await db.Pricings.Where(p=>p.DepartureId==depMap["quote"]&&p.PassengerType==PassengerType.ADULT).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.SalePrice,4000000m));
var changed=await Post(guest,Values("quote",quote.Csrf,quote.Token,"ADULT"));var changedHtml=await changed.Content.ReadAsStringAsync();Check(changed.StatusCode==HttpStatusCode.OK&&changedHtml.Contains("Giá hoặc lịch khởi hành vừa thay đổi"),"price change requires review");
var accepted=await Post(guest,Values("quote",Field(changedHtml,"__RequestVerificationToken"),Field(changedHtml,"Input.SubmissionToken"),"ADULT"));var acceptedCode=Code(accepted);Check(accepted.StatusCode==HttpStatusCode.Redirect&&await db.Bookings.Where(b=>b.BookingCode==acceptedCode).Select(b=>b.TotalPrice).SingleAsync()==4000000,"reviewed quote uses current server price");
using var raceA=Client();using var raceB=Client();var a=await Order(raceA,"race");var b=await Order(raceB,"race");var race=await Task.WhenAll(Post(raceA,Values("race",a.Csrf,a.Token,"ADULT")),Post(raceB,Values("race",b.Csrf,b.Token,"ADULT")));
Check(race.Count(r=>r.StatusCode==HttpStatusCode.Redirect)==1&&race.Count(r=>r.StatusCode==HttpStatusCode.OK)==1,"concurrent booking cannot oversell last seat");
Check(await db.Bookings.CountAsync(x=>x.DepartureId==depMap["race"])==1&&await db.Departures.Where(d=>d.DepartureID==depMap["race"]).Select(d=>d.AvailableSlots).SingleAsync()==0,"one booking and zero remaining seats after race");
var duplicate=await Order(guest,"duplicate");var duplicateValues=Values("duplicate",duplicate.Csrf,duplicate.Token,"ADULT");var dup=await Task.WhenAll(Post(guest,new(duplicateValues)),Post(guest,new(duplicateValues)));
Check(dup.All(r=>r.StatusCode==HttpStatusCode.Redirect)&&Code(dup[0])==Code(dup[1])&&await db.Bookings.CountAsync(x=>x.DepartureId==depMap["duplicate"])==1,"concurrent double submit is idempotent");
async Task SignIn(HttpClient client,string email)
{
    var loginHtml=await(await client.GetAsync("/login")).Content.ReadAsStringAsync();var result=await client.PostAsync("/login",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Field(loginHtml,"__RequestVerificationToken")},{"Email",email},{"Password",f.GetProperty("password").GetString()!}}));Check(result.StatusCode==HttpStatusCode.Redirect,"fixture account login");
}
using var member=Client();await SignIn(member,emails[0]);var own=await Order(member,"owner");Check(own.Html.Contains("Booking Check User")&&own.Html.Contains(emails[0]),"contact prefilled for signed in user");var owned=await Post(member,Values("owner",own.Csrf,own.Token,"ADULT"));var ownedCode=Code(owned);
Check(owned.StatusCode==HttpStatusCode.Redirect&&await db.Bookings.Where(x=>x.BookingCode==ownedCode).Select(x=>x.UserId).SingleAsync()==f.GetProperty("userIds")[0].GetInt32(),"booking owner comes from authenticated identity");
using var otherMember=Client();await SignIn(otherMember,emails[1]);Check((await otherMember.GetAsync(owned.Headers.Location)).StatusCode==HttpStatusCode.NotFound,"other account cannot read booking receipt");
Check((await guest.GetAsync("/order-booking")).StatusCode==HttpStatusCode.BadRequest,"missing booking route parameters handled");Check((await guest.GetAsync("/order-booking?tourCode=CT-HL&departureId="+depMap["main"])).StatusCode==HttpStatusCode.NotFound,"tour and departure must match");
Console.WriteLine("ALL BOOKING CHECKS PASSED");
