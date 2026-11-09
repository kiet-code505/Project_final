using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public partial class DepartureService(AppDbContext db, TimeProvider clock) : IDepartureService
{
    public async Task<DepartureIndexViewModel> GetPagedAsync(DepartureIndexViewModel filter)
    {
        filter.PageSize = filter.PageSize is 10 or 20 or 50 ? filter.PageSize : 10;
        filter.Search = filter.Search?.Trim();
        var query = db.Departures.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.ToLower();
            query = query.Where(d => d.Tour != null &&
                (d.Tour.TourCode.ToLower().Contains(term) || d.Tour.TourName.ToLower().Contains(term)));
        }
        if (filter.TourId.HasValue) query = query.Where(d => d.TourId == filter.TourId.Value);
        if (filter.Status.HasValue) query = filter.Status.Value
            ? query.Where(d => d.Status == true)
            : query.Where(d => d.Status != true);
        if (filter.StartDate.HasValue)
        {
            var start = filter.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(d => d.DepartureDate >= start);
        }
        if (filter.EndDate.HasValue)
        {
            var end = filter.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(d => d.DepartureDate <= end);
        }

        filter.TotalItems = await query.CountAsync();
        filter.ActiveCount = await query.CountAsync(d => d.Status == true);
        filter.AvailableSlots = await query.SumAsync(d => (int?)d.AvailableSlots) ?? 0;
        filter.BookedSlots = await query.SelectMany(d => d.Bookings)
            .Where(b => b.BookingStatus != BookingStatus.CANCELLED && b.BookingStatus != BookingStatus.EXPIRED)
            .SumAsync(b => b.ReservedSeats ?? (int?)b.TotalPassengers) ?? 0;
        filter.Page = Math.Clamp(filter.Page, 1, filter.TotalPages);
        var ordered = filter.NewestFirst
            ? query.OrderByDescending(d => d.DepartureID)
            : query.OrderBy(d => d.DepartureDate).ThenBy(d => d.DepartureID);
        filter.Items = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(d => new DepartureListItem
            {
                DepartureId = d.DepartureID, TourId = d.TourId,
                TourCode = d.Tour == null ? "" : d.Tour.TourCode,
                TourName = d.Tour == null ? "Chưa phân loại tour" : d.Tour.TourName,
                Duration = d.Tour == null ? "" : d.Tour.Duration,
                DepartureDate = d.DepartureDate, Active = d.Status == true,
                AvailableSlots = d.AvailableSlots,
                BookedSlots = d.Bookings.Where(b => b.BookingStatus != BookingStatus.CANCELLED && b.BookingStatus != BookingStatus.EXPIRED)
                    .Sum(b => b.ReservedSeats ?? (int?)b.TotalPassengers) ?? 0,
                LowestPrice = d.Pricings.Where(p => p.PassengerType == PassengerType.ADULT)
                    .Select(p => (decimal?)(p.SalePrice ?? p.OriginalPrice)).Min(),
                HasOutbound = d.Transports.Any(t => t.Type == TransportType.OUTBOUND),
                HasInbound = d.Transports.Any(t => t.Type == TransportType.INBOUND)
            }).ToListAsync();
        return filter;
    }

    public async Task<DepartureDetailViewModel?> GetDetailAsync(int id)
    {
        var departure = await db.Departures.AsNoTracking()
            .Include(d => d.Tour).ThenInclude(t => t!.StartLocation)
            .Include(d => d.Tour).ThenInclude(t => t!.EndLocation)
            .Include(d => d.PolicyTemplate).Include(d => d.Pricings).Include(d => d.Transports)
            .AsSplitQuery().SingleOrDefaultAsync(d => d.DepartureID == id);
        if (departure == null) return null;
        var booked = await db.Bookings.Where(b => b.DepartureId == id && b.BookingStatus != BookingStatus.CANCELLED && b.BookingStatus != BookingStatus.EXPIRED)
            .SumAsync(b => b.ReservedSeats ?? (int?)b.TotalPassengers) ?? 0;
        return new DepartureDetailViewModel { Departure = departure, BookedSlots = booked };
    }
}
