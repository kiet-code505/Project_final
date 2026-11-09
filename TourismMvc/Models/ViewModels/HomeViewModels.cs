using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class HomeTourCard
{
    public int TourId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Duration { get; set; } = "";
    public string StartLocation { get; set; } = "";
    public string EndLocation { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal Discount { get; set; }
    public DateTime? DepartureDate { get; set; }
    public int? AvailableSlots { get; set; }
    public int? DepartureId { get; set; }
    public string DetailUrl => "/tour/" + Uri.EscapeDataString(Code) + (DepartureId.HasValue ? "?departureId=" + DepartureId.Value : "");
    public string DialogId => $"tour-{TourId}-{DepartureDate?.Ticks ?? 0}-{Price?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "_") ?? "contact"}";
}

public class HomeDestination
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ImageUrl { get; set; }
    public Region Region { get; set; }
}

public class HomeIndexViewModel
{
    public List<HomeTourCard> FeaturedTours { get; set; } = [];
    public List<HomeTourCard> SpecialTours { get; set; } = [];
    public List<HomeTourCard> SearchResults { get; set; } = [];
    public List<HomeDestination> Destinations { get; set; } = [];
    public string? Search { get; set; }
    public int? DestinationId { get; set; }
    public string? Budget { get; set; }
    public bool HasSearch => !string.IsNullOrWhiteSpace(Search) || DestinationId.HasValue || !string.IsNullOrEmpty(Budget);
    public int ResultCount { get; set; }
}
