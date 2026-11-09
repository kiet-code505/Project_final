using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Payments;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var folder = @"D:\TourismMvc\artifacts\departure-management-check";
var fixtureFile = Path.Combine(folder, "fixture.json");
var configuration = new ConfigurationBuilder().SetBasePath(@"D:\TourismMvc\TourismMvc").AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true).Build();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(configuration.GetConnectionString("Default")).Options);
var mode = args.FirstOrDefault() ?? "test";
if (mode == "schema")
{
    var columns = await db.Database.SqlQueryRaw<string>("SELECT table_name || '.' || column_name || ' ' || data_type || ' ' || COALESCE(character_maximum_length::text, '') || ' ' || COALESCE(numeric_precision::text, '') || ',' || COALESCE(numeric_scale::text, '') AS \"Value\" FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('departure_pricing','departure_transports','tour_departures')").ToListAsync();
    foreach (var column in columns) Console.WriteLine(column); return;
}
if (mode == "seed")
{
    if (File.Exists(fixtureFile)) throw new Exception("Existing fixture needs cleanup");
    var marker = Guid.NewGuid().ToString("N")[..12]; var password = "Aa1!" + Guid.NewGuid().ToString("N");
    var users = new[] { Role.ADMIN, Role.CUSTOMER }.Select((role, i) => new User { Email = $"departure-check-{marker}-{i}@example.invalid", Password = BCrypt.Net.BCrypt.HashPassword(password), FullName = "Departure Test " + i, Phone = "0901234567", Role = role, Status = true }).ToArray();
    var location = await db.Locations.Select(l => l.LocationID).FirstAsync();
    var policy = await db.Policies.Select(p => p.PolicyTemplateID).FirstAsync();
    var tour = new Tour { TourCode = "DEP-CHECK-" + marker.ToUpperInvariant(), TourName = "Tour kiểm thử lịch khởi hành", Duration = "3 ngày 2 đêm", Transportation = "Xe khách", Attractions = "Fixture", StartLocationId = location, EndLocationId = location };
    db.Tours.Add(tour); db.Users.AddRange(users); await db.SaveChangesAsync();
    await File.WriteAllTextAsync(fixtureFile, JsonSerializer.Serialize(new { marker, password, tourId = tour.TourID, code = tour.TourCode, policyId = policy, emails = users.Select(u => u.Email), userIds = users.Select(u => u.UserID) }));
    Console.WriteLine("Isolated departure fixture prepared."); return;
}
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(fixtureFile)); var fixture = json.RootElement;
var tourId = fixture.GetProperty("tourId").GetInt32(); var code = fixture.GetProperty("code").GetString()!;
if (mode == "cleanup")
{
    if (!code.StartsWith("DEP-CHECK-")) throw new Exception("Unsafe fixture scope");
    await using var tx = await db.Database.BeginTransactionAsync();
    var depIds = await db.Departures.Where(d => d.TourId == tourId && d.Tour!.TourCode == code).Select(d => d.DepartureID).ToListAsync();
    var bookingIds = await db.Bookings.Where(b => depIds.Contains(b.DepartureId)).Select(b => b.BookingID).ToListAsync();
    await db.VnpayPayments.Where(p => bookingIds.Contains(p.BookingId)).ExecuteDeleteAsync();
    await db.BookingPassengers.Where(p => bookingIds.Contains(p.BookingId)).ExecuteDeleteAsync();
    await db.Bookings.Where(b => bookingIds.Contains(b.BookingID)).ExecuteDeleteAsync();
    await db.Pricings.Where(p => depIds.Contains(p.DepartureId)).ExecuteDeleteAsync();
    await db.Transports.Where(t => depIds.Contains(t.DepartureId)).ExecuteDeleteAsync();
    await db.Departures.Where(d => depIds.Contains(d.DepartureID)).ExecuteDeleteAsync();
    await db.Tours.Where(t => t.TourID == tourId && t.TourCode == code).ExecuteDeleteAsync();
    var emails = fixture.GetProperty("emails").EnumerateArray().Select(e => e.GetString()!).ToArray();
    await db.Users.Where(u => emails.Contains(u.Email) && u.Email.StartsWith("departure-check-")).ExecuteDeleteAsync();
    await tx.CommitAsync(); File.Delete(fixtureFile); Console.WriteLine("Isolated departure fixture removed."); return;
}
void Check(bool condition, string label) { if (!condition) throw new Exception("FAILED " + label); Console.WriteLine("PASS " + label); }
HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri("http://localhost:5041") };
string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
async Task Login(HttpClient client, int index)
{
    var path = index == 0 ? "/admin/login" : "/login";
    var page = await client.GetStringAsync(path); var result = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = fixture.GetProperty("emails")[index].GetString()!, ["Password"] = fixture.GetProperty("password").GetString()!, ["__RequestVerificationToken"] = Field(page, "__RequestVerificationToken") }));
    Check(result.StatusCode == HttpStatusCode.Redirect, "login fixture " + index);
}
using var anon = Client(); using var admin = Client(); using var customer = Client();
Check((await anon.GetAsync("/admin/departures/create")).Headers.Location!.OriginalString.StartsWith("/admin/login"), "anonymous admin challenge");
await Login(admin, 0); await Login(customer, 1);
Check((await customer.GetAsync("/admin/departures/create")).StatusCode == HttpStatusCode.Redirect, "customer cannot access departure form");
Check((await customer.PostAsync("/admin/departures/delete-selected", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.Redirect, "customer cannot mutate departures");
if (mode == "extra")
{
    var form = await admin.GetStringAsync("/admin/departures/create?tourId=" + tourId);
    var extraDate = PaymentTime.Now(TimeProvider.System).Date.AddMonths(8);
    var requests = await Task.WhenAll(Post("/admin/departures/create", Values(form, extraDate)), Post("/admin/departures/create", Values(form, extraDate)));
    Check(requests.Count(r => r.StatusCode == HttpStatusCode.Redirect) == 1 && await db.Departures.CountAsync(d => d.TourId == tourId && d.DepartureDate == extraDate) == 1, "concurrent creates cannot duplicate the same tour/date");
    Check((await Post("/admin/departures/create", Values(form, extraDate.AddDays(1)))).StatusCode == HttpStatusCode.Redirect, "second unused departure prepared");
    var selected = await db.Departures.Where(d => d.TourId == tourId && d.DepartureDate >= extraDate).Select(d => d.DepartureID).ToListAsync();
    var fields = selected.Select(id => new KeyValuePair<string,string>("ids", id.ToString())).Append(new("__RequestVerificationToken", Field(form, "__RequestVerificationToken")));
    Check((await admin.PostAsync("/admin/departures/delete-selected", new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.Redirect && !await db.Departures.AnyAsync(d => selected.Contains(d.DepartureID)) && !await db.Transports.AnyAsync(t => selected.Contains(t.DepartureId)) && !await db.Pricings.AnyAsync(p => selected.Contains(p.DepartureId)), "successful bulk delete removes every selected departure and its children");
    Console.WriteLine("Additional concurrency/bulk checks passed."); return;
}
var create = await admin.GetStringAsync("/admin/departures/create?tourId=" + tourId);
Check(create.Contains("Số chỗ còn trống") && create.Contains("departure-airports"), "create form and airport suggestions render");
Check((await admin.PostAsync("/admin/departures/create", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest, "mutations require CSRF token");
var date = PaymentTime.Now(TimeProvider.System).Date.AddMonths(4).AddHours(8);
Dictionary<string, string> Values(string page, DateTime when) {
    var values = new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(page, "__RequestVerificationToken"), ["TourId"] = tourId.ToString(), ["PolicyTemplateId"] = fixture.GetProperty("policyId").GetInt32().ToString(), ["DepartureDate"] = when.ToString("yyyy-MM-ddTHH:mm:ss"), ["AvailableSlots"] = "12", ["TourGuideInfo"] = "Hướng dẫn viên kiểm thử", ["Status"] = "true", ["ReturnUrl"] = "/admin/departures?tourId=" + tourId, ["Revision"] = Field(page, "Revision") };
    foreach (var type in Enum.GetValues<PassengerType>()) { var i = (int)type; values[$"Pricings[{i}].PassengerType"] = type.ToString(); values[$"Pricings[{i}].AgeDescription"] = type.ToString(); values[$"Pricings[{i}].OriginalPrice"] = (1000000 / (i + 1)).ToString(); values[$"Pricings[{i}].SalePrice"] = (900000 / (i + 1)).ToString(); }
    foreach (var type in Enum.GetValues<TransportType>()) { var i = (int)type; values[$"Transports[{i}].Type"] = type.ToString(); values[$"Transports[{i}].Enabled"] = "true"; values[$"Transports[{i}].VehicleType"] = i == 0 ? "BUS" : "TRAIN"; values[$"Transports[{i}].VehicleName"] = "Fixture " + i; values[$"Transports[{i}].TransportCode"] = "CODE" + i; values[$"Transports[{i}].StartPoint"] = "SGN"; values[$"Transports[{i}].EndPoint"] = "DAD"; values[$"Transports[{i}].DepartTime"] = when.AddDays(i * 2).ToString("yyyy-MM-ddTHH:mm:ss"); values[$"Transports[{i}].ArrivalTime"] = when.AddDays(i * 2).AddHours(3).ToString("yyyy-MM-ddTHH:mm:ss"); }
    return values;
}
async Task<HttpResponseMessage> Post(string path, Dictionary<string, string> values) => await admin.PostAsync(path, new FormUrlEncodedContent(values));
var badPrice = Values(create, date); badPrice["Pricings[0].SalePrice"] = "2000000";
var invalid = await Post("/admin/departures/create", badPrice); Check(invalid.IsSuccessStatusCode && WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()).Contains("Giá bán không được vượt giá gốc"), "invalid price redisplays friendly error");
var badTransport = Values(create, date); badTransport["Transports[0].ArrivalTime"] = date.AddHours(-1).ToString("s");
Check(WebUtility.HtmlDecode(await (await Post("/admin/departures/create", badTransport)).Content.ReadAsStringAsync()).Contains("giờ đến phải từ giờ đi"), "invalid transport interval blocked");
var badDate = await Post("/admin/departures/create", Values(create, date.AddYears(-1))); Check(WebUtility.HtmlDecode(await badDate.Content.ReadAsStringAsync()).Contains("trong tương lai"), "past departure blocked");
var submitted = await Post("/admin/departures/create", Values(create, date)); Check(submitted.StatusCode == HttpStatusCode.Redirect, "create complete departure");
var id = int.Parse(submitted.Headers.Location!.OriginalString.Split('?')[0].Split('/').Last());
var detail = await admin.GetStringAsync(submitted.Headers.Location.OriginalString);
Check(detail.Contains("CODE0") && detail.Contains("CODE1") && detail.Contains("900.000"), "details show both directions and saved prices");
var duplicate = await Post("/admin/departures/create", Values(create, date)); Check(duplicate.IsSuccessStatusCode && WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()).Contains("đã có lịch khởi hành"), "duplicate departure blocked");
var editPath = $"/admin/departures/{id}/edit"; var edit = await admin.GetStringAsync(editPath); var saved = Values(edit, date); saved["AvailableSlots"] = "0"; saved["Status"] = "false"; saved["Transports[1].VehicleName"] = "Updated return train";
Check((await Post(editPath, saved)).StatusCode == HttpStatusCode.Redirect, "edit zero remaining seats, status, pricing and transport");
var departure = await db.Departures.AsNoTracking().Include(d => d.Pricings).Include(d => d.Transports).SingleAsync(d => d.DepartureID == id);
Check(departure.AvailableSlots == 0 && departure.Status == false && departure.Pricings.Count == 4 && departure.Transports.Count == 2 && departure.Transports.Single(t => t.Type == TransportType.INBOUND).VehicleName == "Updated return train", "all edit data persisted without duplicate child rows");
var stale = await Post(editPath, Values(edit, date)); Check(stale.IsSuccessStatusCode && WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync()).Contains("Lịch đã thay đổi"), "stale edit rejected");
// Simulate a booking changing remaining seats after the edit page was loaded.
edit = await admin.GetStringAsync(editPath);
await db.Departures.Where(d => d.DepartureID == id).ExecuteUpdateAsync(x => x.SetProperty(d => d.AvailableSlots, 3).SetProperty(d => d.UpdatedAt, DateTime.Now.AddSeconds(2)));
Check(WebUtility.HtmlDecode(await (await Post(editPath, Values(edit, date))).Content.ReadAsStringAsync()).Contains("Lịch đã thay đổi"), "concurrent seat changes cannot be overwritten");
var clonePath = $"/admin/departures/{id}/clone"; var clonePage = await admin.GetStringAsync(clonePath);
Dictionary<string, string> CloneValues(DateTime when) => new() { ["__RequestVerificationToken"] = Field(clonePage, "__RequestVerificationToken"), ["NewDepartureDate"] = when.ToString("s"), ["AvailableSlots"] = "20" };
Check(WebUtility.HtmlDecode(await (await Post(clonePath, CloneValues(date))).Content.ReadAsStringAsync()).Contains("đã có lịch"), "clone duplicate blocked");
var cloned = await Post(clonePath, CloneValues(date.AddDays(9))); Check(cloned.StatusCode == HttpStatusCode.Redirect, "clone departure");
var cloneId = int.Parse(cloned.Headers.Location!.OriginalString.Split('?')[0].Split('/').Last()); var clone = await db.Departures.AsNoTracking().Include(d => d.Pricings).Include(d => d.Transports).SingleAsync(d => d.DepartureID == cloneId);
Check(clone.AvailableSlots == 20 && clone.Status == true && clone.Pricings.Count == 4 && clone.Transports.Count == 2 && clone.Transports.All(t => t.DepartTime >= date.AddDays(9) && t.ArrivalTime == t.DepartTime!.Value.AddHours(3)) && !await db.Bookings.AnyAsync(b => b.DepartureId == cloneId), "clone shifts both transport intervals and copies no bookings");
db.Bookings.Add(new Booking { DepartureId = id, BookingCode = "DC" + Guid.NewGuid().ToString("N")[..12], BookingStatus = BookingStatus.CANCELLED, ContactFullName = "Fixture", ContactEmail = "departure-check@example.invalid", ContactPhone = "0901234567", ContactAddress = "Fixture", TotalPassengers = 1 }); await db.SaveChangesAsync();
var list = await admin.GetStringAsync("/admin/departures?tourId=" + tourId); var token = Field(list, "__RequestVerificationToken");
var deleted = await Post($"/admin/departures/{id}/delete", new() { ["__RequestVerificationToken"] = token }); Check(deleted.StatusCode == HttpStatusCode.Redirect && WebUtility.HtmlDecode(await admin.GetStringAsync(deleted.Headers.Location!.OriginalString)).Contains("Không thể xóa lịch đã có booking"), "single delete with booking returns friendly message");
var bulk = await admin.PostAsync("/admin/departures/delete-selected", new FormUrlEncodedContent(new[] { new KeyValuePair<string,string>("__RequestVerificationToken",token), new("ids",id.ToString()), new("ids",cloneId.ToString()) }));
Check(bulk.StatusCode == HttpStatusCode.Redirect && await db.Departures.CountAsync(d => d.DepartureID == id || d.DepartureID == cloneId) == 2, "bulk delete is atomic when one departure has bookings");
var csv = await admin.GetAsync("/admin/departures/export?tourId=" + tourId); Check(csv.Content.Headers.ContentType!.MediaType == "text/csv" && (await csv.Content.ReadAsStringAsync()).Contains(code), "filtered CSV export");
var allowed = await Post($"/admin/departures/{cloneId}/delete", new() { ["__RequestVerificationToken"] = token });
Check(allowed.StatusCode == HttpStatusCode.Redirect && !await db.Departures.AnyAsync(d => d.DepartureID == cloneId) && !await db.Pricings.AnyAsync(p => p.DepartureId == cloneId) && !await db.Transports.AnyAsync(t => t.DepartureId == cloneId), "unused departure deletion removes prices and transport");
var invalidFilter = await admin.GetAsync("/admin/departures?startDate=invalid"); Check(invalidFilter.IsSuccessStatusCode, "invalid filter does not throw 500");
Console.WriteLine("All departure management HTTP/database checks passed.");
