using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using Microsoft.Extensions.Options;
using TourismMvc.Payments;

namespace TourismMvc.Services;

public class BookingOrderService(AppDbContext db, IOptions<VnpayOptions> paymentOptions, TimeProvider clock) : IBookingOrderService
{
    private static DateTime Today => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time").Date;
    private static string Quote(TourDeparture departure, IEnumerable<DeparturePricing> prices) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        departure.DepartureDate.ToString("O") + "|" + string.Join("|", prices.OrderBy(p => p.PassengerType).ThenBy(p => p.PricingID)
            .Select(p => $"{p.PassengerType}:{p.OriginalPrice.ToString(CultureInfo.InvariantCulture)}:{p.SalePrice?.ToString(CultureInfo.InvariantCulture)}")))));

    public async Task<BookingTourInfo?> GetInfoAsync(string tourCode, int departureId)
    {
        var code = tourCode.Trim().ToUpperInvariant();
        var departure = await db.Departures.AsNoTracking()
            .Where(d => d.DepartureID == departureId && d.Tour != null && d.Tour.TourCode == code && d.Tour.Status && d.Tour.IsDeleted != true && d.IsDeleted != true)
            .Include(d => d.Tour).ThenInclude(t => t!.Images)
            .Include(d => d.Tour).ThenInclude(t => t!.StartLocation)
            .Include(d => d.Tour).ThenInclude(t => t!.EndLocation)
            .Include(d => d.Transports).Include(d => d.Pricings).AsSplitQuery().SingleOrDefaultAsync();
        if (departure?.Tour == null) return null;
        var prices = departure.Pricings.Where(p => p.PassengerType is PassengerType.ADULT or PassengerType.CHILD or PassengerType.INFANT).ToList();
        return new BookingTourInfo
        {
            TourId = departure.TourId, TourCode = departure.Tour.TourCode, TourName = departure.Tour.TourName, Duration = departure.Tour.Duration,
            StartLocation = departure.Tour.StartLocation?.LocationName ?? "", EndLocation = departure.Tour.EndLocation?.LocationName ?? "",
            ImageUrl = departure.Tour.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder).Select(i => i.ImageURL).FirstOrDefault(),
            Departure = departure, Prices = prices, QuoteStamp = Quote(departure, prices),
            CanBook = departure.Status == true && departure.DepartureDate >= Today && departure.AvailableSlots > 0
                && prices.Any(p => p.PassengerType == PassengerType.ADULT && p.EffectivePrice >= 0)
        };
    }

    public async Task<bool> PrefillAsync(BookingInputModel input, int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId && u.Status);
        if (user == null) return false;
        var latest = await db.Bookings.AsNoTracking().Where(b => b.UserId == userId).OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.BookingID).FirstOrDefaultAsync();
        input.ContactFullName = latest?.ContactFullName ?? user.FullName;
        input.ContactEmail = latest?.ContactEmail ?? user.Email;
        input.ContactPhone = latest?.ContactPhone ?? user.Phone ?? "";
        input.ContactAddress = latest?.ContactAddress ?? "";
        return true;
    }

    public async Task<Booking?> CreateAsync(BookingInputModel input, BookingSubmission submission, ModelStateDictionary modelState)
    {
        if (!modelState.IsValid) return null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize reservations for one departure and recheck the submission after acquiring the lock.
        var departure = await db.Departures.FromSqlInterpolated($"SELECT * FROM tour_departures WHERE departureid = {input.DepartureId} FOR UPDATE").SingleOrDefaultAsync();
        var existing = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingCode == submission.Code);
        if (existing != null)
        {
            if (existing.DepartureId == input.DepartureId && existing.UserId == submission.UserId) return existing;
            modelState.AddModelError("", "Phiên đặt tour không hợp lệ. Vui lòng tải lại trang."); return null;
        }
        var tour = departure == null ? null : await db.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.TourID == departure.TourId && t.TourCode == input.TourCode.Trim().ToUpperInvariant() && t.Status && t.IsDeleted != true);
        if (departure == null || tour == null || departure.Status != true || departure.IsDeleted == true || departure.DepartureDate < Today)
        { modelState.AddModelError("", "Lịch khởi hành không còn nhận đặt chỗ. Vui lòng chọn lịch khác."); return null; }
        var prices = await db.Pricings.AsNoTracking().Where(p => p.DepartureId == departure.DepartureID && (p.PassengerType == PassengerType.ADULT || p.PassengerType == PassengerType.CHILD || p.PassengerType == PassengerType.INFANT)).ToListAsync();
        if (Quote(departure, prices) != submission.QuoteStamp)
        { modelState.AddModelError("", "Giá hoặc lịch khởi hành vừa thay đổi. Vui lòng kiểm tra lại thông tin và tổng tiền trước khi gửi lại."); return null; }
        if (input.Passengers.Count is < 1 or > 50)
        { modelState.AddModelError("", "Mỗi yêu cầu đặt tour cần từ 1 đến 50 hành khách."); return null; }
        var adults = input.Passengers.Count(p => p.PassengerType == "ADULT");
        var infants = input.Passengers.Count(p => p.PassengerType == "INFANT");
        if (adults < 1) modelState.AddModelError("", "Cần có ít nhất một người lớn trong đoàn.");
        if (infants > adults) modelState.AddModelError("", "Mỗi người lớn chỉ đi kèm tối đa một em bé dưới 2 tuổi.");
        var seats = input.Passengers.Count(p => p.PassengerType != "INFANT");
        if (seats > departure.AvailableSlots) modelState.AddModelError("", $"Chuyến đi chỉ còn {departure.AvailableSlots} chỗ. Vui lòng giảm số hành khách hoặc chọn lịch khác.");
        var passengers = new List<BookingPassenger>();
        for (var i = 0; i < input.Passengers.Count; i++)
        {
            var person = input.Passengers[i];
            var birth = person.DateOfBirth;
            var date = DateOnly.FromDateTime(departure.DepartureDate);
            var age = birth.HasValue ? date.Year - birth.Value.Year - (date < birth.Value.AddYears(date.Year - birth.Value.Year) ? 1 : 0) : -1;
            if (!birth.HasValue || birth > DateOnly.FromDateTime(Today) || age > 120 || age < 0)
                modelState.AddModelError($"Input.Passengers[{i}].DateOfBirth", $"Ngày sinh hành khách {i + 1} không hợp lệ.");
            else if (!(person.PassengerType == "ADULT" && age >= 12 || person.PassengerType == "CHILD" && age >= 2 && age < 12 || person.PassengerType == "INFANT" && age < 2))
                modelState.AddModelError($"Input.Passengers[{i}].DateOfBirth", $"Độ tuổi hành khách {i + 1} chưa phù hợp với loại vé, tính theo ngày khởi hành.");
            var price = prices.FirstOrDefault(p => p.PassengerType.ToString() == person.PassengerType);
            if (price == null || price.EffectivePrice < 0) { modelState.AddModelError("", $"Chưa có giá hợp lệ cho loại hành khách {person.PassengerType}."); continue; }
            passengers.Add(new BookingPassenger { FullName = person.FullName.Trim(), Gender = person.Gender, DateOfBirth = birth ?? default,
                PassengerType = person.PassengerType, BasePrice = price.EffectivePrice });
        }
        if (!modelState.IsValid) return null;
        var total = passengers.Sum(p => p.BasePrice);
        var booking = new Booking
        {
            BookingCode = submission.Code, BookingDate = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time"),
            ContactFullName = input.ContactFullName.Trim(), ContactEmail = input.ContactEmail.Trim(), ContactPhone = input.ContactPhone.Trim(),
            ContactAddress = input.ContactAddress.Trim(), CustomerNote = input.CustomerNote?.Trim(),
            DepartureId = departure.DepartureID, UserId = submission.UserId, Passengers = passengers, TotalPassengers = passengers.Count,
            SubtotalPrice = total, TotalPrice = total,
            BookingStatus = total > 0 ? BookingStatus.PENDING_PAYMENT : BookingStatus.PENDING_CONFIRMATION,
            PaymentDeadline = total > 0 ? PaymentTime.Now(clock).AddMinutes(Math.Clamp(paymentOptions.Value.HoldMinutes, 5, 30)) : null,
            ReservedSeats = seats
        };
        departure.AvailableSlots -= seats;
        db.Bookings.Add(booking); await db.SaveChangesAsync(); await transaction.CommitAsync();
        return booking;
    }

    public Task<Booking?> GetConfirmationAsync(string code) => db.Bookings.AsNoTracking().Where(b => b.BookingCode == code)
        .Include(b => b.Passengers).Include(b => b.TourDeparture).ThenInclude(d => d!.Tour).AsSplitQuery().SingleOrDefaultAsync();
}
