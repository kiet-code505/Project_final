using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IPublicTourDetailService
{
    Task<PublicTourDetailViewModel?> GetAsync(string tourCode, int? departureId);
}
