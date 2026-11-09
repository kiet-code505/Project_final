using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Payments;
using TourismMvc.Services;

static class PaymentChecks
{
    static readonly string FilePath = @"D:\TourismMvc\artifacts\vnpay-check\fixture.json";
    static string Connection = "";
    static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options);
    public static void Check(bool ok, string label) { if (!ok) throw new Exception("FAILED " + label); Console.WriteLine("PASS " + label); }
    public static async Task RunAsync(string mode, string connection)
    {
        Connection = connection;
        if (mode == "seed") { await SeedAsync(); return; }
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(FilePath));
        var fixture = json.RootElement;
        var tourId = fixture.GetProperty("tourId").GetInt32();
        var ids = fixture.GetProperty("userIds").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var deps = fixture.GetProperty("departures").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var code = fixture.GetProperty("code").GetString()!;
        if (mode.StartsWith("admin-", StringComparison.Ordinal)) { await AdminBookingChecks.RunAsync(mode, connection, fixture); return; }
        if (mode == "cleanup")
        {
            if (!code.StartsWith("PAY-CHECK-", StringComparison.Ordinal)) throw new Exception("Unsafe cleanup scope");
            await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync();
            var owned = await db.Bookings.Where(b => deps.Contains(b.DepartureId)).Select(b => b.BookingID).ToListAsync();
            await db.VnpayPayments.Where(p => owned.Contains(p.BookingId)).ExecuteDeleteAsync();
            await db.BookingPassengers.Where(p => owned.Contains(p.BookingId)).ExecuteDeleteAsync();
            await db.Bookings.Where(b => owned.Contains(b.BookingID)).ExecuteDeleteAsync();
            await db.Pricings.Where(p => deps.Contains(p.DepartureId)).ExecuteDeleteAsync();
            await db.Departures.Where(d => deps.Contains(d.DepartureID) && d.TourId == tourId).ExecuteDeleteAsync();
            await db.Tours.Where(t => t.TourID == tourId && t.TourCode == code).ExecuteDeleteAsync();
            await db.Users.Where(u => ids.Contains(u.UserID) && u.Email.StartsWith("vnpay-check-")).ExecuteDeleteAsync();
            await tx.CommitAsync(); File.Delete(FilePath); Console.WriteLine("Temporary payment fixture removed."); return;
        }
        if (mode == "test") await DomainAsync(code, deps, ids[1]);
        if (mode == "extra") await DomainAsync(code, deps, ids[1], true);
        if (mode == "http") await HttpAsync(fixture);
        if (mode == "assert-live-paid")
        {
            using var paid=JsonDocument.Parse(await File.ReadAllTextAsync(@"D:\TourismMvc\artifacts\vnpay-check\actual-paid-result.json"));
            var bookingCode=paid.RootElement.GetProperty("receipt").GetString()!.Split('/').Last();
            var reference=paid.RootElement.GetProperty("reference").GetString()!;
            var providerNo=paid.RootElement.GetProperty("providerTransactionNo").GetString()!;
            await using var db=Db();var payment=await db.VnpayPayments.AsNoTracking().Include(p=>p.Booking).ThenInclude(b=>b!.TourDeparture).SingleAsync(p=>p.TransactionRef==reference);
            Check(payment.Booking!.BookingCode==bookingCode&&deps.Contains(payment.Booking.DepartureId),"real Sandbox result belongs to isolated fixture");
            Check(payment.Status==VnpayPaymentStatus.SUCCEEDED&&payment.Booking.BookingStatus==BookingStatus.PAID&&payment.ProviderTransactionNo==providerNo&&payment.Amount==10000&&payment.SettledAt.HasValue,"database records authentic Sandbox settlement and provider transaction number");
            Check(payment.Booking.ReservedSeats==1&&!payment.Booking.SeatsReleased&&payment.Booking.TourDeparture!.AvailableSlots==9,"successful payment reserves exactly one seat without double subtraction");
            Check(await db.VnpayPayments.CountAsync(p=>p.BookingId==payment.BookingId)==1,"one gateway payment attempt for native checkout");
            Console.WriteLine(JsonSerializer.Serialize(new{payment.SettledAt,payment.LastCheckedAt,IpnConfirmedWithoutQueryAfterPayment=payment.LastCheckedAt==null||payment.LastCheckedAt<payment.SettledAt}));
        }
        if (mode == "inspect-live")
        {
            using var result=JsonDocument.Parse(await File.ReadAllTextAsync(@"D:\TourismMvc\artifacts\vnpay-check\browser-result.json"));
            var bookingCode=result.RootElement.GetProperty("receipt").GetString()!.Split('/').Last();
            await using var db=Db();var payment=await db.VnpayPayments.AsNoTracking().Include(p=>p.Booking).SingleAsync(p=>p.Booking!.BookingCode==bookingCode);
            if(!deps.Contains(payment.Booking!.DepartureId))throw new Exception("Not an isolated UI fixture");
            Console.WriteLine(JsonSerializer.Serialize(new{BookingStatus=payment.Booking.BookingStatus.ToString(),PaymentStatus=payment.Status.ToString(),payment.CreatedAt,payment.ExpiresAt,payment.LastCheckedAt}));
            var config=new ConfigurationBuilder().SetBasePath(@"D:\TourismMvc\TourismMvc").AddJsonFile("appsettings.Vnpay.json").AddJsonFile("appsettings.Vnpay.Development.local.json").Build();
            var settings=new VnpayOptions();config.GetSection("Vnpay").Bind(settings);
            using var http=new HttpClient(new InspectQueryHandler(settings.HashSecret)){Timeout=TimeSpan.FromSeconds(15)};
            if(Environment.GetEnvironmentVariable("VNPAY_CHECK_USER_AGENT")=="1")http.DefaultRequestHeaders.UserAgent.ParseAdd("TourismMvc/1.0");
            var query=new VnpayQueryClient(http,Options.Create(settings),TimeProvider.System,NullLogger<VnpayQueryClient>.Instance);
            var notice=await query.QueryAsync(payment,CancellationToken.None);
            Console.WriteLine("Verified query result: "+(notice==null?"none":notice.TransactionStatus));
        }
        if (mode == "notify-ui")
        {
            using var result=JsonDocument.Parse(await File.ReadAllTextAsync(@"D:\TourismMvc\artifacts\vnpay-check\browser-result.json"));
            var bookingCode=result.RootElement.GetProperty("receipt").GetString()!.Split('/').Last();
            await using var db=Db();var payment=await db.VnpayPayments.AsNoTracking().Include(p=>p.Booking).SingleAsync(p=>p.Booking!.BookingCode==bookingCode);
            if(!deps.Contains(payment.Booking!.DepartureId)||payment.Amount!=10000||payment.Status!=VnpayPaymentStatus.PENDING)throw new Exception("Not an isolated pending UI fixture");
            var config=new ConfigurationBuilder().SetBasePath(@"D:\TourismMvc\TourismMvc").AddJsonFile("appsettings.Vnpay.json").AddJsonFile("appsettings.Vnpay.Development.local.json").Build();
            var secret=config["Vnpay:HashSecret"]!;
            var notice=new Dictionary<string,string>{{"vnp_TmnCode",payment.MerchantCode},{"vnp_TxnRef",payment.TransactionRef},{"vnp_Amount","1000000"},{"vnp_ResponseCode","00"},{"vnp_TransactionStatus","00"},{"vnp_TransactionNo","987654321"},{"vnp_BankCode","NCB"},{"vnp_PayDate",VnpayProtocol.Date(PaymentTime.Now(TimeProvider.System))}};
            using var http=new HttpClient();var body=await http.GetStringAsync("http://localhost:5037/payment/vnpay/ipn?"+Sign(notice,secret));
            Check(body.Contains("\"00\""),"HTTP signed callback simulation for isolated UI fixture only");
        }
    }
    static async Task SeedAsync()
    {
        if (File.Exists(FilePath)) throw new Exception("Clean existing fixture first");
        await using var db = Db(); var marker = Guid.NewGuid().ToString("N")[..12];
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        var users = Enumerable.Range(0, 2).Select(i => new User { Email = $"vnpay-check-{marker}-{i}@example.invalid", FullName = "Payment Test " + i, Phone = "0901234567", Password = BCrypt.Net.BCrypt.HashPassword(password), Role = i == 0 ? Role.ADMIN : Role.CUSTOMER }).ToArray();
        var places = await db.Locations.OrderBy(l => l.LocationID).Select(l => l.LocationID).Take(2).ToListAsync();
        var policy = await db.Policies.Select(p => p.PolicyTemplateID).FirstAsync();
        var tour = new Tour { TourCode = "PAY-CHECK-" + marker.ToUpperInvariant(), TourName = "Tour kiểm thử thanh toán VNPAY", Duration = "3 ngày 2 đêm", Transportation = "Xe khách", Attractions = "Kiểm thử tạm", StartLocationId = places[0], EndLocationId = places[^1], Status = true };
        var deps = Enumerable.Range(0, 10).Select(i => new TourDeparture { Tour = tour, DepartureDate = PaymentTime.Now(TimeProvider.System).Date.AddMonths(2).AddDays(i), AvailableSlots = 10, Status = true, PolicyTemplateId = policy, TourGuideInfo = "Payment test guide" }).ToArray();
        foreach (var dep in deps)
            foreach (var entry in new[] { (PassengerType.ADULT, 10000m), (PassengerType.CHILD, 5000m), (PassengerType.INFANT, 1000m) })
                dep.Pricings.Add(new DeparturePricing { PassengerType = entry.Item1, OriginalPrice = entry.Item2, AgeDescription = "Test" });
        db.Users.AddRange(users); db.Departures.AddRange(deps); await db.SaveChangesAsync();
        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(new { code = tour.TourCode, tourId = tour.TourID, userIds = users.Select(u => u.UserID), emails = users.Select(u => u.Email), password, departures = deps.Select(d => d.DepartureID) }));
        Console.WriteLine("Temporary isolated payment tour, departures and accounts prepared.");
    }
    static readonly VnpayOptions TestOptions = new() { TmnCode = "TESTMVC1", HashSecret = "only-for-isolated-fixture-tests-123456", PublicBaseUrl = "http://localhost:5037", HoldMinutes = 15, SettlementGraceMinutes = 2 };
    static VnpayPaymentService Service(AppDbContext db, TimeProvider clock, HttpMessageHandler? handler = null) => new(db, Options.Create(TestOptions), clock,
        new VnpayQueryClient(new HttpClient(handler ?? new QueryHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))), Options.Create(TestOptions), clock, NullLogger<VnpayQueryClient>.Instance));
    static string Sign(Dictionary<string, string> values, string? secret = null) => VnpayProtocol.Canonical(values) + "&vnp_SecureHash=" + VnpayProtocol.Hash(secret ?? TestOptions.HashSecret, VnpayProtocol.Canonical(values));
    static IQueryCollection Notice(VnpayPayment p, DateTime now, string response = "00", string status = "00", decimal? amount = null, string? merchant = null) => new QueryCollection(QueryHelpers.ParseQuery(Sign(new()
    {
        ["vnp_TmnCode"] = merchant ?? p.MerchantCode, ["vnp_TxnRef"] = p.TransactionRef,
        ["vnp_Amount"] = ((long)((amount ?? p.Amount) * 100)).ToString(CultureInfo.InvariantCulture), ["vnp_ResponseCode"] = response,
        ["vnp_TransactionStatus"] = status, ["vnp_TransactionNo"] = "123456789", ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = VnpayProtocol.Date(now)
    })));
    static async Task DomainAsync(string code, int[] deps, int userId, bool extendedOnly = false)
    {
        Check(VnpayProtocol.Hash(new string('\x0b', 20), "Hi There") == "87aa7cdea5ef619d4ff0b4241a1d6cb02379f4e2ce4ec2787ad0b30545e17cde" + "daa833b7d6b8a702038b274eaea3f4e4be9d914eeb61f1702e696c203a126854", "HMAC-SHA512 RFC 4231 vector");
        Check(VnpayProtocol.Canonical(new Dictionary<string,string>{{"vnp_ReturnUrl","http://localhost:5019/payment/vnpay/return"},{"vnp_OrderInfo","Thanh toan tour BK123"}}) == "vnp_OrderInfo=Thanh+toan+tour+BK123&vnp_ReturnUrl=http%3A%2F%2Flocalhost%3A5019%2Fpayment%2Fvnpay%2Freturn", "VNPAY sorted URL-encoded canonical format");
        var clock = new TestClock(); var initial = clock.GetUtcNow();
        async Task<Booking> Create(int index)
        {
            clock.Utc = initial;
            await using var db = Db(); var orders = new BookingOrderService(db, Options.Create(TestOptions), clock); var info = await orders.GetInfoAsync(code, deps[index]);
            var input = new BookingInputModel { TourCode = code, DepartureId = deps[index], ContactFullName = "Payment test", ContactEmail = "payment-test@example.invalid", ContactPhone = "0901234567", ContactAddress = "Test address", AgreeToPolicy = true, Passengers = new()
            {
                new() { FullName = "Adult fixture", Gender = "Nam", PassengerType = "ADULT", DateOfBirth = DateOnly.FromDateTime(info!.Departure.DepartureDate).AddYears(-30) },
                new() { FullName = "Infant fixture", Gender = "Nữ", PassengerType = "INFANT", DateOfBirth = DateOnly.FromDateTime(info.Departure.DepartureDate).AddYears(-1) }
            } };
            var state = new ModelStateDictionary(); var booking = await orders.CreateAsync(input, new BookingSubmission("BK" + Convert.ToHexString(RandomNumberGenerator.GetBytes(9)), deps[index], info.QuoteStamp, userId, "fixture-browser"), state);
            Check(booking != null && state.IsValid && booking.BookingStatus == BookingStatus.PENDING_PAYMENT && booking.ReservedSeats == 1 && booking.PaymentDeadline.HasValue, "new booking holds seats once and sets payment deadline " + index);
            return booking!;
        }
        async Task<VnpayPayment> Start(Booking b)
        {
            await using var db = Db(); var r = await Service(db, clock).StartAsync(b.BookingID, "::1"); Check(r.Url?.StartsWith(VnpayOptions.GatewayUrl, StringComparison.Ordinal) == true, "creates VNPAY Sandbox URL");
            var q = QueryHelpers.ParseQuery(new Uri(r.Url!).Query); Check(q["vnp_Amount"] == "1100000" && q["vnp_IpAddr"] == "127.0.0.1" && q["vnp_ReturnUrl"] == TestOptions.PublicBaseUrl + "/payment/vnpay/return", "amount uses server total x100, IPv4 loopback and configured callback");
            return await db.VnpayPayments.AsNoTracking().SingleAsync(p => p.TransactionRef == q["vnp_TxnRef"].ToString());
        }
        async Task<VnpayNotificationResult> Send(IQueryCollection query) { await using var db = Db(); return await Service(db, clock).HandleIpnAsync(query); }
        async Task<int> Slots(int index) { await using var db = Db(); return await db.Departures.Where(d => d.DepartureID == deps[index]).Select(d => d.AvailableSlots).SingleAsync(); }
        async Task<Booking> Saved(Booking b) { await using var db = Db(); return await db.Bookings.AsNoTracking().SingleAsync(x => x.BookingID == b.BookingID); }
        if (!extendedOnly)
        {
        var main = await Create(0); var p = await Start(main);
        var duplicateStarts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { await using var db = Db(); return await Service(db, clock).StartAsync(main.BookingID, "127.0.0.1"); }));
        await using (var db = Db()) Check(await db.VnpayPayments.CountAsync(x => x.BookingId == main.BookingID) == 1 && duplicateStarts.All(x => x.Url != null), "double click reuses one pending payment attempt");
        var signed = Notice(p, PaymentTime.Now(clock)); var tampered = new QueryCollection(signed.ToDictionary(x => x.Key, x => x.Value)); tampered = new QueryCollection(tampered.ToDictionary(x => x.Key, x => x.Key == "vnp_Amount" ? new Microsoft.Extensions.Primitives.StringValues("1") : x.Value));
        Check((await Send(tampered)).RspCode == "97", "tampered amount invalidates checksum");
        Check((await Send(Notice(p, PaymentTime.Now(clock), amount: 1))).RspCode == "04", "valid signature with wrong amount rejected");
        Check((await Send(Notice(p, PaymentTime.Now(clock), merchant: "OTHER"))).RspCode == "97", "wrong merchant rejected");
        Check((await Send(new QueryCollection(QueryHelpers.ParseQuery(Sign(signed.ToDictionary(x => x.Key, x => x.Value.ToString()).Where(x => x.Key != "vnp_SecureHash").ToDictionary(x => x.Key, x => x.Value)) + "&vnp_Amount=1100000")))).RspCode == "97", "duplicate signed parameters rejected");
        Check((await Send(Notice(p, PaymentTime.Now(clock).AddDays(1)))).RspCode == "99", "invalid settlement date rejected");
        Check((await Saved(main)).BookingStatus == BookingStatus.PENDING_PAYMENT && await Slots(0) == 9, "invalid callbacks do not mutate booking or seats");
        var notifications = await Task.WhenAll(Send(signed), Send(signed));
        Check(notifications.Count(x => x.RspCode == "00") == 1 && notifications.Count(x => x.RspCode == "02") == 1 && (await Saved(main)).BookingStatus == BookingStatus.PAID && await Slots(0) == 9, "concurrent success callbacks mark paid once without subtracting seats again");
        await using (var db = Db()) Check((await Service(db, clock).StartAsync(main.BookingID, "127.0.0.1")).Url == null, "paid booking cannot create another payment");
        var retry = await Create(1); var old = await Start(retry); await Send(Notice(old, PaymentTime.Now(clock), "24", "02"));
        Check((await Saved(retry)).BookingStatus == BookingStatus.PENDING_PAYMENT && await Slots(1) == 9, "failed payment keeps reservation available for retry");
        var next = await Start(retry); Check(next.TransactionRef != old.TransactionRef, "retry after failure gets a new reference");
        await Send(Notice(old, PaymentTime.Now(clock))); await Send(Notice(next, PaymentTime.Now(clock)));
        await using (var db = Db()) Check((await Saved(retry)).BookingStatus == BookingStatus.PAID && await db.VnpayPayments.AnyAsync(x => x.Id == next.Id && x.Status == VnpayPaymentStatus.REVIEW_REQUIRED) && await Slots(1) == 9, "late success can recover failed attempt; a second charge requires review");
        var late = await Create(2); var latePay = await Start(late); var paidTime = PaymentTime.Now(clock); clock.Utc = initial.AddMinutes(20);
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { await using var db = Db(); await Service(db, clock).ExpireAsync(late.BookingCode); }));
        Check((await Saved(late)).BookingStatus == BookingStatus.EXPIRED && (await Saved(late)).SeatsReleased && await Slots(2) == 10, "concurrent expiry releases only noninfant seats once");
        await Send(Notice(latePay, paidTime)); await using (var db = Db()) Check((await Saved(late)).BookingStatus == BookingStatus.EXPIRED && await db.VnpayPayments.AnyAsync(x => x.Id == latePay.Id && x.Status == VnpayPaymentStatus.REVIEW_REQUIRED) && await Slots(2) == 10, "capture after release is recorded for review without overselling");
        var queried = await Create(3); var qp = await Start(queried);
        HttpMessageHandler QueryResponse(bool valid) => new QueryHandler(request =>
        {
            var data = new Dictionary<string,string> { ["vnp_ResponseId"]="fixture", ["vnp_Command"]="querydr", ["vnp_ResponseCode"]="00", ["vnp_Message"]="Success", ["vnp_TmnCode"]=qp.MerchantCode, ["vnp_TxnRef"]=qp.TransactionRef, ["vnp_Amount"]="1100000", ["vnp_BankCode"]="NCB", ["vnp_PayDate"]=VnpayProtocol.Date(PaymentTime.Now(clock)), ["vnp_TransactionNo"]="99887766", ["vnp_TransactionType"]="01", ["vnp_TransactionStatus"]="00", ["vnp_OrderInfo"]="fixture query", ["vnp_PromotionCode"]="", ["vnp_PromotionAmount"]="" };
            var text=string.Join("|",new[]{"fixture","querydr","00","Success",qp.MerchantCode,qp.TransactionRef,"1100000","NCB",data["vnp_PayDate"],"99887766","01","00","fixture query","",""});
            data["vnp_SecureHash"]=valid?Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(TestOptions.HashSecret),Encoding.UTF8.GetBytes(text))).ToLowerInvariant():new string('0',128);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=JsonContent.Create(data) };
        });
        await using(var db=Db()) await Service(db,clock,QueryResponse(false)).RefreshAsync(queried.BookingCode);
        Check((await Saved(queried)).BookingStatus==BookingStatus.PENDING_PAYMENT,"forged provider query response rejected");
        clock.Utc=initial.AddSeconds(16);await using(var db=Db()) await Service(db,clock,QueryResponse(true)).RefreshAsync(queried.BookingCode);
        Check((await Saved(queried)).BookingStatus==BookingStatus.PAID&&await Slots(3)==9,"server query reconciles successful payment when IPN is unavailable");
        var redirected=await Create(4);var rp=await Start(redirected);await using(var db=Db()) await Service(db,clock).HandleReturnAsync(Notice(rp,PaymentTime.Now(clock)));
        Check((await Saved(redirected)).BookingStatus==BookingStatus.PENDING_PAYMENT,"signed browser return alone cannot mark booking paid");
        }
        var race=await Create(5);var racePayment=await Start(race);var raceDate=PaymentTime.Now(clock);clock.Utc=initial.AddMinutes(20);
        await Task.WhenAll(Send(Notice(racePayment,raceDate)),Task.Run(async()=>{await using var db=Db();await Service(db,clock).ExpireAsync(race.BookingCode);}));
        var raceSaved=await Saved(race);Check(raceSaved.BookingStatus==BookingStatus.PAID&&!raceSaved.SeatsReleased&&await Slots(5)==9||raceSaved.BookingStatus==BookingStatus.EXPIRED&&raceSaved.SeatsReleased&&await Slots(5)==10,"expiry racing successful IPN preserves seat accounting");
        var untouched=await Create(6);await using(var db=Db())await db.Bookings.Where(b=>b.BookingID==untouched.BookingID).ExecuteUpdateAsync(s=>s.SetProperty(b=>b.BookingStatus,BookingStatus.PENDING_CONFIRMATION).SetProperty(b=>b.PaymentDeadline,(DateTime?)null));
        clock.Utc=initial.AddDays(10);await using(var db=Db())await Service(db,clock).ExpireAsync(untouched.BookingCode);
        Check((await Saved(untouched)).BookingStatus==BookingStatus.PENDING_CONFIRMATION&&await Slots(6)==9,"legacy booking with no payment deadline is never autoexpired");
        Console.WriteLine("All payment domain and database checks passed.");
    }
    static async Task HttpAsync(JsonElement fixture)
    {
        using var guest = new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,CookieContainer=new CookieContainer() }) { BaseAddress=new Uri("http://localhost:5037") };
        string Field(string html,string name) => WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var code=fixture.GetProperty("code").GetString()!;var dep=fixture.GetProperty("departures")[8].GetInt32();
        var html=await guest.GetStringAsync("/order-booking?tourCode="+code+"&departureId="+dep);
        var values=new Dictionary<string,string>{{"__RequestVerificationToken",Field(html,"__RequestVerificationToken")},{"Input.TourCode",code},{"Input.DepartureId",dep.ToString()},{"Input.SubmissionToken",Field(html,"Input.SubmissionToken")},{"Input.ContactFullName","Guest VNPAY Fixture"},{"Input.ContactEmail","guest-payment@example.invalid"},{"Input.ContactPhone","0901234567"},{"Input.ContactAddress","Test address"},{"Input.AgreeToPolicy","true"},{"Input.Passengers[0].FullName","Adult Fixture"},{"Input.Passengers[0].Gender","Nam"},{"Input.Passengers[0].DateOfBirth","1990-01-01"},{"Input.Passengers[0].PassengerType","ADULT"}};
        var created=await guest.PostAsync("/order-booking",new FormUrlEncodedContent(values));Check(created.StatusCode==HttpStatusCode.Redirect,"HTTP guest booking created");var receipt=created.Headers.Location!.OriginalString;
        var page=await guest.GetStringAsync(receipt);Check(page.Contains("pay-vnpay")&&page.Contains("Chờ thanh toán")&&page.Contains("Sandbox"),"private checkout page contains payment button and deadline");
        Check((await guest.PostAsync(receipt+"/pay",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"payment creation requires antiforgery token");
        using var stranger=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=guest.BaseAddress};Check((await stranger.GetAsync(receipt)).StatusCode==HttpStatusCode.NotFound&&(await stranger.GetAsync(receipt+"/status")).StatusCode==HttpStatusCode.NotFound,"receipt and polling remain private to booking owner");
        var start=await guest.PostAsync(receipt+"/pay",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Field(page,"__RequestVerificationToken")},{"Amount","1"}}));Check(start.StatusCode==HttpStatusCode.Redirect&&start.Headers.Location!.Host=="sandbox.vnpayment.vn","native payment POST redirects to Sandbox");
        var query=QueryHelpers.ParseQuery(start.Headers.Location!.Query);Check(query["vnp_Amount"]=="1000000"&&query["vnp_ReturnUrl"]=="http://localhost:5037/payment/vnpay/return","HTTP amount tampering ignored and callback configured");
        var invalid=await guest.GetAsync("/payment/vnpay/return?vnp_ResponseCode=00&vnp_TxnRef=guess&vnp_SecureHash=bad");Check(invalid.StatusCode==HttpStatusCode.BadRequest,"forged return shows friendly 400");
        var ipn=await guest.GetStringAsync("/payment/vnpay/ipn?vnp_ResponseCode=00&vnp_SecureHash=bad");Check(ipn.Contains("97"),"forged IPN rejected with VNPAY acknowledgement");
        Check((await stranger.GetAsync("/admin/payments")).StatusCode==HttpStatusCode.Redirect,"admin payments protected");
        await File.WriteAllTextAsync(@"D:\TourismMvc\artifacts\vnpay-check\http-result.json",JsonSerializer.Serialize(new{receipt,reference=query["vnp_TxnRef"].ToString()}));
        Console.WriteLine("All HTTP payment checks passed.");
    }
}
class TestClock : TimeProvider { public DateTimeOffset Utc=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Utc; }
class QueryHandler(Func<HttpRequestMessage,HttpResponseMessage> response) : HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(response(request)); }
class InspectQueryHandler(string secret) : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        if(Environment.GetEnvironmentVariable("VNPAY_CHECK_TRANSACTION_NO")=="1"||Environment.GetEnvironmentVariable("VNPAY_CHECK_NUMERIC_REQUEST")=="1")
        {
            var payload=System.Text.Json.Nodes.JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!;
            if(Environment.GetEnvironmentVariable("VNPAY_CHECK_TRANSACTION_NO")=="1")payload["vnp_TransactionNo"]="";
            if(Environment.GetEnvironmentVariable("VNPAY_CHECK_NUMERIC_REQUEST")=="1")payload["vnp_RequestId"]=DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            var fields=new[]{"vnp_RequestId","vnp_Version","vnp_Command","vnp_TmnCode","vnp_TxnRef","vnp_TransactionDate","vnp_CreateDate","vnp_IpAddr","vnp_OrderInfo"};
            payload["vnp_SecureHash"]=VnpayProtocol.Hash(secret,string.Join("|",fields.Select(key=>payload[key]?.ToString()??"")));
            request.Content=new StringContent(payload.ToJsonString(),Encoding.UTF8,"application/json");
        }
        var response=await base.SendAsync(request,token);var body=await response.Content.ReadAsStringAsync(token);
        Console.WriteLine("Provider HTTP status: "+(int)response.StatusCode);
        try
        {
            using var json=JsonDocument.Parse(body);var root=json.RootElement;
            string Field(string name)=>root.TryGetProperty(name,out var field)&&field.ValueKind!=JsonValueKind.Null?field.ToString():"";
            var fields=new[]{"vnp_ResponseId","vnp_Command","vnp_ResponseCode","vnp_Message","vnp_TmnCode","vnp_TxnRef","vnp_Amount","vnp_BankCode","vnp_PayDate","vnp_TransactionNo","vnp_TransactionType","vnp_TransactionStatus","vnp_OrderInfo"};
            var raw=string.Join("|",fields.Select(Field));var received=Field("vnp_SecureHash");
            Console.WriteLine(JsonSerializer.Serialize(new{Code=Field("vnp_ResponseCode"),Message=Field("vnp_Message"),Status=Field("vnp_TransactionStatus"),PayDate=Field("vnp_PayDate"),Keys=root.EnumerateObject().Select(p=>p.Name),BaseSignature=VnpayProtocol.Matches(received,VnpayProtocol.Hash(secret,raw)),PromotionSignature=VnpayProtocol.Matches(received,VnpayProtocol.Hash(secret,raw+"|"+Field("vnp_PromotionCode")+"|"+Field("vnp_PromotionAmount")))}));
        }
        catch(JsonException){Console.WriteLine(JsonSerializer.Serialize(new{Type=response.Content.Headers.ContentType?.MediaType,Server=response.Headers.Server.ToString(),Title=Regex.Match(body,@"<title[^>]*>(.*?)</title>",RegexOptions.IgnoreCase).Groups[1].Value,ShortText=body.Length<180?body:"non-JSON response"}));}
        return response;
    }
}
