using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IHomeService
{
    Task<HomeIndexViewModel> GetHomeAsync(string? search, int? destinationId, string? budget);
}
