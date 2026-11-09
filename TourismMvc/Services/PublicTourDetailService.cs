using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class PublicTourDetailService(AppDbContext db) : IPublicTourDetailService
{
    public async Task<PublicTourDetailViewModel?> GetAsync(string tourCode, int? departureId)
    {
        var code = tourCode.Trim().ToUpperInvariant();
        var tour = await db.Tours.AsNoTracking().Where(t => t.TourCode == code && t.Status && t.IsDeleted != true)
            .Include(t => t.StartLocation).Include(t => t.EndLocation)
            .Include(t => t.Images).Include(t => t.ItineraryDays).AsSplitQuery().SingleOrDefaultAsync();
        if (tour == null) return null;
        var today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time").Date;
        var departures = await db.Departures.AsNoTracking()
            .Where(d => d.TourId == tour.TourID && d.Status == true && d.IsDeleted != true && d.DepartureDate >= today)
            .Include(d => d.Pricings).Include(d => d.Transports)
            .Include(d => d.PolicyTemplate).ThenInclude(p => p!.Contact)
            .OrderBy(d => d.DepartureDate).ThenBy(d => d.DepartureID).AsSplitQuery().ToListAsync();
        var selected = departures.FirstOrDefault(d => d.DepartureID == departureId);
        var headerPrice = selected != null ? selected.Pricings.FirstOrDefault(p => p.PassengerType == PassengerType.ADULT)
            : departures.Where(d => d.AvailableSlots > 0).SelectMany(d => d.Pricings)
                .Where(p => p.PassengerType == PassengerType.ADULT).OrderBy(p => p.EffectivePrice).FirstOrDefault();
        var policy = departures.Select(d => d.PolicyTemplate).FirstOrDefault(p => p != null && p.IsDeleted != true);
        if (policy == null)
        {
            var fallbackDeparture = await db.Departures.AsNoTracking()
                .Where(d => d.TourId == tour.TourID && d.Status == true && d.IsDeleted != true
                    && d.PolicyTemplate != null && d.PolicyTemplate.IsDeleted != true)
                .Include(d => d.PolicyTemplate).ThenInclude(p => p!.Contact)
                .OrderByDescending(d => d.DepartureDate).FirstOrDefaultAsync();
            policy = fallbackDeparture?.PolicyTemplate;
        }

        var related = await db.Tours.AsNoTracking()
            .Where(t => t.TourID != tour.TourID && t.EndLocationId == tour.EndLocationId && t.Status && t.IsDeleted != true)
            .OrderByDescending(t => t.TourID).Take(3)
            .Select(t => new HomeTourCard
            {
                TourId = t.TourID, Code = t.TourCode, Name = t.TourName, Duration = t.Duration,
                StartLocation = t.StartLocation == null ? "" : t.StartLocation.LocationName,
                ImageUrl = t.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder).Select(i => i.ImageURL).FirstOrDefault(),
                Price = t.Departures.Where(d => d.Status == true && d.IsDeleted != true && d.AvailableSlots > 0 && d.DepartureDate >= today)
                    .SelectMany(d => d.Pricings).Where(p => p.PassengerType == PassengerType.ADULT)
                    .Select(p => (decimal?)(p.SalePrice ?? p.OriginalPrice)).Min()
            }).ToListAsync();
        return new PublicTourDetailViewModel
        {
            Tour = tour, Images = tour.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder).ThenBy(i => i.ImageID)
                .Select(i => i.ImageURL).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList(),
            Departures = departures, SelectedDepartureId = selected?.DepartureID,
            InvalidDeparture = departureId.HasValue && selected == null,
            HeaderPrice = headerPrice, DefaultPolicy = policy, RelatedTours = related
        };
    }
}
