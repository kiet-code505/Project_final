using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class PublicTourDetailViewModel
{
    public required Tour Tour { get; init; }
    public List<string> Images { get; init; } = [];
    public List<TourDeparture> Departures { get; init; } = [];
    public int? SelectedDepartureId { get; init; }
    public bool InvalidDeparture { get; init; }
    public DeparturePricing? HeaderPrice { get; init; }
    public PolicyTemplate? DefaultPolicy { get; init; }
    public List<HomeTourCard> RelatedTours { get; init; } = [];
}
