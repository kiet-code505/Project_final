using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class PublicTourSearchService(AppDbContext db) : IPublicTourSearchService
{
    public async Task<PublicTourSearchViewModel> SearchAsync(PublicTourSearchFilter filter)
    {
        filter.SearchNameTour = filter.SearchNameTour?.Trim();
        filter.Transportation = filter.Transportation?.Trim();
        filter.StartLocationID = filter.StartLocationID > 0 ? filter.StartLocationID : null;
        filter.EndLocationID = filter.EndLocationID > 0 ? filter.EndLocationID : null;
        filter.Budget = filter.Budget switch
        {
            "under5" or "Dưới 5 triệu" => "under5",
            "5to10" or "Từ 5 - 10 triệu" => "5to10",
            "10to20" or "Từ 10 - 20 triệu" => "10to20",
            "over20" or "Trên 20 triệu" => "over20",
            _ => null
        };
        filter.Sort = filter.Sort is "price-asc" or "price-desc" or "name" ? filter.Sort : "newest";
        var vm = new PublicTourSearchViewModel { Filter = filter };
        var today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time").Date;
        var allTours = db.Tours.AsNoTracking().Where(t => t.Status && t.IsDeleted != true);
        var query = allTours;
        if (!string.IsNullOrWhiteSpace(filter.SearchNameTour))
        {
            var term = filter.SearchNameTour.ToLower();
            query = query.Where(t => t.TourName.ToLower().Contains(term) || t.TourCode.ToLower().Contains(term)
                || (t.EndLocation != null && t.EndLocation.LocationName.ToLower().Contains(term)));
        }
        if (filter.StartLocationID.HasValue) query = query.Where(t => t.StartLocationId == filter.StartLocationID);
        if (filter.EndLocationID.HasValue) query = query.Where(t => t.EndLocationId == filter.EndLocationID);
        if (!string.IsNullOrWhiteSpace(filter.Transportation))
        {
            var transport = filter.Transportation.ToLower();
            // Hai cách viết thường gặp trong dữ liệu tiếng Việt.
            query = transport is "tàu hỏa" or "tàu hoả"
                ? query.Where(t => t.Transportation.ToLower().Contains("tàu hỏa") || t.Transportation.ToLower().Contains("tàu hoả"))
                : query.Where(t => t.Transportation.ToLower().Contains(transport));
        }
        var cards = query.Select(t => new PublicTourSearchItem
        {
            TourId = t.TourID, Code = t.TourCode, Name = t.TourName, Duration = t.Duration,
            Transportation = t.Transportation,
            StartLocation = t.StartLocation == null ? "" : t.StartLocation.LocationName,
            EndLocation = t.EndLocation == null ? "" : t.EndLocation.LocationName,
            ImageUrl = t.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder).ThenBy(i => i.ImageID)
                .Select(i => i.ImageURL).FirstOrDefault(),
            Price = t.Departures.Where(d => d.Status == true && d.IsDeleted != true && d.DepartureDate >= today && d.AvailableSlots > 0)
                .SelectMany(d => d.Pricings).Where(p => p.PassengerType == PassengerType.ADULT)
                .Select(p => (decimal?)(p.SalePrice ?? p.OriginalPrice)).Min(),
            DepartureDate = t.Departures.Where(d => d.Status == true && d.IsDeleted != true && d.DepartureDate >= today && d.AvailableSlots > 0)
                .OrderBy(d => d.DepartureDate).Select(d => (DateTime?)d.DepartureDate).FirstOrDefault()
        });
        cards = filter.Budget switch
        {
            "under5" => cards.Where(c => c.Price < 5000000),
            "5to10" => cards.Where(c => c.Price >= 5000000 && c.Price < 10000000),
            "10to20" => cards.Where(c => c.Price >= 10000000 && c.Price <= 20000000),
            "over20" => cards.Where(c => c.Price > 20000000),
            _ => cards
        };
        vm.TotalItems = await cards.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, vm.TotalPages);
        var sorted = filter.Sort switch
        {
            "price-asc" => cards.OrderBy(c => c.Price == null).ThenBy(c => c.Price).ThenBy(c => c.TourId),
            "price-desc" => cards.OrderBy(c => c.Price == null).ThenByDescending(c => c.Price).ThenBy(c => c.TourId),
            "name" => cards.OrderBy(c => c.Name).ThenBy(c => c.TourId),
            _ => cards.OrderByDescending(c => c.TourId)
        };
        vm.Items = await sorted.Skip((filter.Page - 1) * vm.PageSize).Take(vm.PageSize).ToListAsync();
        var locations = db.Locations.AsNoTracking().Where(l => l.Status && l.IsDeleted != true);
        vm.Destinations = await locations.Where(l => allTours.Any(t => t.EndLocationId == l.LocationID))
            .OrderBy(l => l.LocationName).Select(l => new HomeDestination { Id = l.LocationID, Name = l.LocationName }).ToListAsync();
        vm.StartLocations = await locations.Where(l => allTours.Any(t => t.StartLocationId == l.LocationID))
            .OrderBy(l => l.LocationName).Select(l => new HomeDestination { Id = l.LocationID, Name = l.LocationName }).ToListAsync();
        return vm;
    }
}
