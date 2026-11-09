using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class HomeService(AppDbContext db) : IHomeService
{
    public async Task<HomeIndexViewModel> GetHomeAsync(string? search, int? destinationId, string? budget)
    {
        var today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time").Date;
        var tours = db.Tours.AsNoTracking().Where(t => t.Status && t.IsDeleted != true);
        var vm = new HomeIndexViewModel
        {
            Search = search?.Trim(), DestinationId = destinationId,
            Budget = budget is "under5" or "5to10" or "10to20" or "over20" ? budget : null
        };
        var cards = tours.Select(t => new HomeTourCard
        {
            TourId = t.TourID, Code = t.TourCode, Name = t.TourName, Duration = t.Duration,
            StartLocation = t.StartLocation == null ? "" : t.StartLocation.LocationName,
            EndLocation = t.EndLocation == null ? "" : t.EndLocation.LocationName,
            ImageUrl = t.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder)
                .Select(i => i.ImageURL).FirstOrDefault(),
            Price = t.Departures.Where(d => d.Status == true && d.IsDeleted != true && d.DepartureDate >= today && d.AvailableSlots > 0)
                .SelectMany(d => d.Pricings).Where(p => p.PassengerType == PassengerType.ADULT)
                .Select(p => (decimal?)(p.SalePrice ?? p.OriginalPrice)).Min(),
            DepartureDate = t.Departures.Where(d => d.Status == true && d.IsDeleted != true && d.DepartureDate >= today && d.AvailableSlots > 0)
                .OrderBy(d => d.DepartureDate).Select(d => (DateTime?)d.DepartureDate).FirstOrDefault()
        });
        vm.FeaturedTours = await cards.OrderByDescending(t => t.TourId).Take(5).ToListAsync();

        // Mỗi tour lấy lịch khởi hành có phần trăm giảm giá người lớn cao nhất.
        var offers = db.Pricings.AsNoTracking().Where(p => p.PassengerType == PassengerType.ADULT
            && p.OriginalPrice > 0 && p.SalePrice.HasValue && p.SalePrice < p.OriginalPrice
            && p.TourDeparture != null && p.TourDeparture.Status == true && p.TourDeparture.IsDeleted != true
            && p.TourDeparture.AvailableSlots > 0 && p.TourDeparture.DepartureDate >= today
            && p.TourDeparture.Tour != null && p.TourDeparture.Tour.Status && p.TourDeparture.Tour.IsDeleted != true);
        var bestOffers = offers.Where(p => !offers.Any(other => other.TourDeparture!.TourId == p.TourDeparture!.TourId
            && ((other.OriginalPrice - other.SalePrice!.Value) / other.OriginalPrice > (p.OriginalPrice - p.SalePrice!.Value) / p.OriginalPrice
                || ((other.OriginalPrice - other.SalePrice!.Value) / other.OriginalPrice == (p.OriginalPrice - p.SalePrice!.Value) / p.OriginalPrice
                    && (other.TourDeparture.DepartureDate < p.TourDeparture.DepartureDate
                        || (other.TourDeparture.DepartureDate == p.TourDeparture.DepartureDate && other.PricingID < p.PricingID))))));
        vm.SpecialTours = await bestOffers
            .OrderByDescending(p => (p.OriginalPrice - p.SalePrice!.Value) / p.OriginalPrice).Take(7)
            .Select(p => new HomeTourCard
            {
                TourId = p.TourDeparture!.TourId, Code = p.TourDeparture.Tour!.TourCode,
                Name = p.TourDeparture.Tour.TourName, Duration = p.TourDeparture.Tour.Duration,
                StartLocation = p.TourDeparture.Tour.StartLocation == null ? "" : p.TourDeparture.Tour.StartLocation.LocationName,
                EndLocation = p.TourDeparture.Tour.EndLocation == null ? "" : p.TourDeparture.Tour.EndLocation.LocationName,
                ImageUrl = p.TourDeparture.Tour.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder).Select(i => i.ImageURL).FirstOrDefault(),
                Price = p.SalePrice, OriginalPrice = p.OriginalPrice,
                Discount = (p.OriginalPrice - p.SalePrice!.Value) / p.OriginalPrice * 100,
                DepartureDate = p.TourDeparture.DepartureDate, AvailableSlots = p.TourDeparture.AvailableSlots,
                DepartureId = p.TourDeparture.DepartureID
            }).ToListAsync();

        vm.Destinations = await db.Locations.AsNoTracking().Where(l => l.Status && l.IsDeleted != true)
            .OrderByDescending(l => tours.Any(t => t.EndLocationId == l.LocationID)).ThenBy(l => l.LocationName)
            .Select(l => new HomeDestination { Id = l.LocationID, Name = l.LocationName, ImageUrl = l.ImageURL, Region = l.Region })
            .ToListAsync();
        if (vm.HasSearch)
        {
            var results = cards;
            if (destinationId.HasValue)
                results = results.Where(c => tours.Any(t => t.TourID == c.TourId && t.EndLocationId == destinationId));
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var term = vm.Search.ToLower();
                results = results.Where(c => c.Name.ToLower().Contains(term) || c.Code.ToLower().Contains(term) || c.EndLocation.ToLower().Contains(term));
            }
            results = vm.Budget switch
            {
                "under5" => results.Where(c => c.Price < 5000000),
                "5to10" => results.Where(c => c.Price >= 5000000 && c.Price < 10000000),
                "10to20" => results.Where(c => c.Price >= 10000000 && c.Price <= 20000000),
                "over20" => results.Where(c => c.Price > 20000000),
                _ => results
            };
            vm.ResultCount = await results.CountAsync();
            vm.SearchResults = await results.OrderByDescending(c => c.TourId).Take(24).ToListAsync();
        }
        return vm;
    }
}
