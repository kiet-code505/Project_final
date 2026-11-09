using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Payments;
using TourismMvc.Services;

static class AdminBookingChecks
{
    const string MapPath=@"D:\TourismMvc\artifacts\vnpay-check\admin-cases.json";
    static string Connection="";
    static AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options);
    static void Check(bool ok,string label)=>PaymentChecks.Check(ok,label);
    static readonly VnpayOptions Gateway=new(){TmnCode="TESTMVC1",HashSecret="admin-booking-isolated-test-secret"};
    public static async Task RunAsync(string mode,string connection,JsonElement fixture)
    {
        Connection=connection;
        var deps=fixture.GetProperty("departures").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        var marker=fixture.GetProperty("code").GetString()!.Replace("PAY-CHECK-","");
        if(mode=="admin-seed")
        {
            await using var db=Db();
            if(await db.Bookings.AnyAsync(b=>deps.Contains(b.DepartureId)))throw new Exception("Fixture already has bookings");
            var now=PaymentTime.Now(TimeProvider.System);var bookings=new List<Booking>();
            for(var i=0;i<12;i++)
            {
                var dep=await db.Departures.SingleAsync(d=>d.DepartureID==deps[i%10]);
                if(i==6)dep.DepartureDate=now.Date.AddDays(-1);
                var types=i==9?new[]{"ADULT","CHILD","INFANT"}:i==0?new[]{"ADULT","INFANT"}:new[]{"ADULT"};
                var people=types.Select((type,j)=>new BookingPassenger{FullName="Test Passenger "+j,Gender="Nam",DateOfBirth=DateOnly.FromDateTime(dep.DepartureDate).AddYears(type=="ADULT"?-30:type=="CHILD"?-7:-1),PassengerType=type,BasePrice=type=="ADULT"?10000:type=="CHILD"?5000:1000}).ToList();
                var seats=types.Count(x=>x!="INFANT");
                var status=i==1?BookingStatus.PAID:i is 2 or 3 or 6 or 9 or 11?BookingStatus.PENDING_CONFIRMATION:BookingStatus.PENDING_PAYMENT;
                var booking=new Booking{BookingCode="BK"+Convert.ToHexString(RandomNumberGenerator.GetBytes(9)),BookingDate=now.Date.AddDays(-(i%4)).AddHours(10),ContactFullName=i==3?"<script>fixture</script>":"Admin Booking Fixture "+i,ContactEmail="admin-booking-"+marker+"@example.invalid",ContactPhone="0901234567",ContactAddress="Fixture address",CustomerNote="<script>alert('fixture')</script>",TotalPassengers=people.Count,TotalPrice=people.Sum(p=>p.BasePrice),SubtotalPrice=people.Sum(p=>p.BasePrice),BookingStatus=status,DepartureId=dep.DepartureID,UserId=fixture.GetProperty("userIds")[1].GetInt32(),ReservedSeats=i==9?null:seats,PaymentDeadline=status==BookingStatus.PENDING_PAYMENT?now.AddMinutes(15):null,Passengers=people};
                dep.AvailableSlots-=seats;
                if(i is 1 or 4 or 5 or 8)
                    booking.Payments.Add(new VnpayPayment{TransactionRef="VP"+Guid.NewGuid().ToString("N"),MerchantCode=Gateway.TmnCode,Amount=booking.TotalPrice,Status=i==1?VnpayPaymentStatus.SUCCEEDED:i==4?VnpayPaymentStatus.REVIEW_REQUIRED:VnpayPaymentStatus.PENDING,CreatedAt=now,ExpiresAt=now.AddMinutes(15),SettledAt=i is 1 or 4?now:null,ProviderTransactionNo=i is 1 or 4?"1234500"+i:null});
                bookings.Add(booking);db.Bookings.Add(booking);
            }
            await db.SaveChangesAsync();await File.WriteAllTextAsync(MapPath,JsonSerializer.Serialize(new{marker,cases=bookings.Select((b,i)=>new{index=i,id=b.BookingID,code=b.BookingCode,departureId=b.DepartureId})}));Console.WriteLine("Twelve isolated admin booking cases prepared.");return;
        }
        using var mapping=JsonDocument.Parse(await File.ReadAllTextAsync(MapPath));
        var cases=mapping.RootElement.GetProperty("cases").EnumerateArray().Select(x=>x.GetProperty("id").GetInt32()).ToArray();
        if(mode=="admin-domain")await DomainAsync(cases,marker);
        if(mode=="admin-http")await HttpAsync(cases,marker,fixture);
    }
    static async Task<Booking> Saved(int id){await using var db=Db();return await db.Bookings.AsNoTracking().Include(b=>b.Payments).Include(b=>b.TourDeparture).SingleAsync(b=>b.BookingID==id);}
    static async Task<(bool Success,string Message)> Cancel(int id,BookingStatus expected){await using var db=Db();return await new AdminBookingService(db,TimeProvider.System).CancelAsync(id,expected);}
    static async Task<(bool Success,string Message)> Confirm(int id,BookingStatus expected){await using var db=Db();return await new AdminBookingService(db,TimeProvider.System).ConfirmPaymentAsync(id,expected);}
    static VnpayPaymentService PaymentService(AppDbContext db)=>new(db,Options.Create(Gateway),TimeProvider.System,new VnpayQueryClient(new HttpClient(new QueryHandler(_=>new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))),Options.Create(Gateway),TimeProvider.System,NullLogger<VnpayQueryClient>.Instance));
    static async Task SendSuccess(int id)
    {
        await using var db=Db();var payment=await db.VnpayPayments.AsNoTracking().SingleAsync(p=>p.BookingId==id);
        var fields=new Dictionary<string,string>{{"vnp_TmnCode",Gateway.TmnCode},{"vnp_TxnRef",payment.TransactionRef},{"vnp_Amount",((long)(payment.Amount*100)).ToString()},{"vnp_ResponseCode","00"},{"vnp_TransactionStatus","00"},{"vnp_TransactionNo",(80000000+payment.Id).ToString()},{"vnp_PayDate",VnpayProtocol.Date(PaymentTime.Now(TimeProvider.System))},{"vnp_BankCode","NCB"}};
        var query=VnpayProtocol.Canonical(fields);var result=await PaymentService(db).HandleIpnAsync(new QueryCollection(QueryHelpers.ParseQuery(query+"&vnp_SecureHash="+VnpayProtocol.Hash(Gateway.HashSecret,query))));
        Check(result.RspCode=="00","isolated success callback accepted");
    }
    static async Task DomainAsync(int[] ids,string marker)
    {
        await using(var db=Db())
        {
            var service=new AdminBookingService(db,TimeProvider.System);var page=await service.SearchAsync(new AdminBookingFilter{Search=marker.ToLower(),PageSize=10});
            Check(page.TotalItems==12&&page.Items.Count==10&&page.TotalPages==2,"case-insensitive search, paging and count");
            var last=await service.SearchAsync(new AdminBookingFilter{Search=marker,Page=int.MaxValue,PageSize=-1});Check(last.Filter.Page==2&&last.Items.Count==2,"page and page size normalized");
            var today=DateOnly.FromDateTime(PaymentTime.Now(TimeProvider.System));var dated=await service.SearchAsync(new AdminBookingFilter{Search=marker,FromDate=today,ToDate=today});Check(dated.TotalItems==3,"booking date range includes entire day");
            var paid=await service.SearchAsync(new AdminBookingFilter{Search=marker,Status=BookingStatus.PAID});Check(paid.TotalItems==1,"booking status filter");
            var detail=await service.GetDetailsAsync(ids[0]);Check(detail?.Booking.Passengers.Count==2&&detail.AccountName!=null&&detail.CanCancel&&!detail.CanConfirm,"details include travelers and account, online booking cannot be manually confirmed");
        }
        var before=await Saved(ids[0]);var slots=before.TourDeparture!.AvailableSlots;
        Check((await Cancel(ids[0],BookingStatus.PENDING_PAYMENT)).Success,"unpaid booking cancellation");
        var after=await Saved(ids[0]);Check(after.BookingStatus==BookingStatus.CANCELLED&&after.SeatsReleased&&after.TourDeparture!.AvailableSlots==slots+1,"cancellation returns one adult seat, infant excluded");
        await Cancel(ids[0],BookingStatus.PENDING_PAYMENT);Check((await Saved(ids[0])).TourDeparture!.AvailableSlots==slots+1,"repeat cancellation does not return seats twice");
        before=await Saved(ids[1]);Check(!(await Cancel(ids[1],BookingStatus.PAID)).Success,"paid cancellation rejected server-side");after=await Saved(ids[1]);Check(after.BookingStatus==BookingStatus.PAID&&after.TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots&&after.Payments.Single().Status==VnpayPaymentStatus.SUCCEEDED,"paid booking, money record and seats preserved");
        before=await Saved(ids[2]);Check((await Confirm(ids[2],BookingStatus.PENDING_CONFIRMATION)).Success,"legacy confirmation of collected payment");await Confirm(ids[2],BookingStatus.PENDING_CONFIRMATION);after=await Saved(ids[2]);Check(after.BookingStatus==BookingStatus.PAID&&after.TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots,"manual confirmation is idempotent and retains reservation");
        Check(!(await Cancel(ids[4],BookingStatus.PENDING_PAYMENT)).Success,"unresolved captured payment blocks cancellation");
        Check(!(await Cancel(ids[3],BookingStatus.PENDING_PAYMENT)).Success&&(await Saved(ids[3])).BookingStatus==BookingStatus.PENDING_CONFIRMATION,"stale confirmation screen rejected");
        Check(!(await Confirm(ids[10],BookingStatus.PENDING_PAYMENT)).Success,"online unpaid booking cannot be marked paid manually");
        before=await Saved(ids[5]);Check((await Cancel(ids[5],BookingStatus.PENDING_PAYMENT)).Success,"cancellation with pending gateway attempt");after=await Saved(ids[5]);Check(after.Payments.Single().Status==VnpayPaymentStatus.EXPIRED,"pending payment attempt closed locally on cancellation");await SendSuccess(ids[5]);after=await Saved(ids[5]);Check(after.BookingStatus==BookingStatus.CANCELLED&&after.Payments.Single().Status==VnpayPaymentStatus.REVIEW_REQUIRED&&after.TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots+1,"late success after cancellation is retained for review without reserving again");
        Check(!(await Cancel(ids[6],BookingStatus.PENDING_CONFIRMATION)).Success&&!(await Confirm(ids[6],BookingStatus.PENDING_CONFIRMATION)).Success,"departed tour cannot be cancelled or manually confirmed");
        before=await Saved(ids[7]);await using(var db=Db())await db.Bookings.Where(b=>b.BookingID==ids[7]).ExecuteUpdateAsync(s=>s.SetProperty(b=>b.PaymentDeadline,PaymentTime.Now(TimeProvider.System).AddMinutes(-20)));
        await Task.WhenAll(Cancel(ids[7],BookingStatus.PENDING_PAYMENT),Task.Run(async()=>{await using var db=Db();await PaymentService(db).ExpireAsync(before.BookingCode);}));after=await Saved(ids[7]);Check(after.SeatsReleased&&after.BookingStatus is BookingStatus.CANCELLED or BookingStatus.EXPIRED&&after.TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots+1,"expiry racing cancellation releases seats once");
        before=await Saved(ids[8]);await Task.WhenAll(Cancel(ids[8],BookingStatus.PENDING_PAYMENT),SendSuccess(ids[8]));after=await Saved(ids[8]);Check(after.BookingStatus==BookingStatus.PAID&&!after.SeatsReleased&&after.TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots||after.BookingStatus==BookingStatus.CANCELLED&&after.SeatsReleased&&after.TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots+1&&after.Payments.Single().Status==VnpayPaymentStatus.REVIEW_REQUIRED,"VNPAY success racing cancellation preserves valid state and capacity");
        before=await Saved(ids[9]);Check((await Cancel(ids[9],BookingStatus.PENDING_CONFIRMATION)).Success&&(await Saved(ids[9])).TourDeparture!.AvailableSlots==before.TourDeparture!.AvailableSlots+2,"legacy reservation restores adult and child seats, excludes infant");
        Check(!(await Cancel(-1,BookingStatus.PENDING_PAYMENT)).Success,"missing booking gives friendly error");
        Console.WriteLine("All admin booking domain checks passed.");
    }
    static async Task HttpAsync(int[] ids,string marker,JsonElement fixture)
    {
        HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new CookieContainer()}){BaseAddress=new Uri("http://localhost:5038")};
        string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        async Task Login(HttpClient client,int i){var path=i==0?"/admin/login":"/login";var html=await client.GetStringAsync(path);var response=await client.PostAsync(path,new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",fixture.GetProperty("emails")[i].GetString()!},{"Password",fixture.GetProperty("password").GetString()!},{"__RequestVerificationToken",Token(html)}}));Check(response.StatusCode==HttpStatusCode.Redirect,"fixture login "+i);}
        using var guest=Client();using var admin=Client();using var customer=Client();Check((await guest.GetAsync("/admin/bookings")).Headers.Location!.OriginalString.StartsWith("/admin/login"),"anonymous challenged at admin login");await Login(admin,0);await Login(customer,1);
        Check((await customer.GetAsync("/admin/bookings")).StatusCode==HttpStatusCode.Redirect&&(await customer.PostAsync("/admin/bookings/"+ids[3]+"/cancel",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.Redirect,"customer cannot view or mutate admin bookings");
        var list=await admin.GetStringAsync("/admin/bookings?search="+marker);Check(Regex.Matches(list,"data-booking-id=").Count==10&&list.Contains("rel=\"next\""),"HTTP list and pagination");
        Check((await admin.GetStringAsync("/admin/bookings?search="+marker+"&page=2147483647&pageSize=-1")).Contains("data-booking-id="),"HTTP oversized paging does not fail");
        Check((await admin.GetStringAsync("/admin/bookings?fromDate=2026-11-10&toDate=2026-10-01")).Contains("Ngày kết thúc phải"),"invalid date range shows validation");
        var detail=await admin.GetStringAsync("/admin/bookings/"+ids[3]);Check(detail.Contains("&lt;script&gt;")&&!detail.Contains("<script>alert('fixture')"),"contact and notes encoded");Check(!detail.Contains("$2a$")&&!detail.Contains("$2b$"),"account password hash never rendered");
        var paidDetail=await admin.GetStringAsync("/admin/bookings/"+ids[1]);Check(!paidDetail.Contains("id=\"cancel-booking\"")&&!paidDetail.Contains("id=\"confirm-booking\""),"paid booking has no cancel or manual-confirm action");
        var confirm=await admin.GetStringAsync("/admin/bookings/"+ids[3]+"/cancel");Check((await Saved(ids[3])).BookingStatus==BookingStatus.PENDING_CONFIRMATION,"GET confirmation does not mutate booking");
        Check((await admin.PostAsync("/admin/bookings/"+ids[3]+"/cancel",new FormUrlEncodedContent(new Dictionary<string,string>{{"ExpectedStatus","PENDING_CONFIRMATION"}}))).StatusCode==HttpStatusCode.BadRequest,"cancel requires antiforgery token");
        var response=await admin.PostAsync("/admin/bookings/"+ids[3]+"/cancel",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Token(confirm)},{"ExpectedStatus","PENDING_CONFIRMATION"},{"ReturnUrl","https://example.invalid"},{"TotalPrice","1"},{"BookingStatus","PAID"}}));
        Check(response.StatusCode==HttpStatusCode.Redirect&&response.Headers.Location!.OriginalString=="/admin/bookings"&&(await Saved(ids[3])).BookingStatus==BookingStatus.CANCELLED&&(await Saved(ids[3])).TotalPrice==10000,"POST cancellation ignores tampered prices/status and rejects external redirects");
        var denied=await admin.GetStringAsync("/admin/bookings/"+ids[1]+"/cancel");response=await admin.PostAsync("/admin/bookings/"+ids[1]+"/cancel",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Token(denied)},{"ExpectedStatus","PAID"}}));Check(response.StatusCode==HttpStatusCode.Redirect&&(await Saved(ids[1])).BookingStatus==BookingStatus.PAID,"forged cancel of paid booking rejected");
        var missing=await admin.GetAsync("/admin/bookings/2147483647");Check(missing.StatusCode==HttpStatusCode.Redirect,"missing detail redirects with friendly message");
        Check(!list.Contains("refund",StringComparison.OrdinalIgnoreCase)&&!detail.Contains("refund",StringComparison.OrdinalIgnoreCase),"no refund workflow in booking pages");
        Console.WriteLine("All admin booking HTTP checks passed.");
    }
}
