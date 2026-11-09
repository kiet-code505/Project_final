using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IPublicTourSearchService
{
    Task<PublicTourSearchViewModel> SearchAsync(PublicTourSearchFilter filter);
}
