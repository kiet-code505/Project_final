using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface ITourService
{
    Task<TourIndexViewModel> GetPagedToursAsync(
        string? search,
        int? startLocId,
        int? endLocId,
        bool? status,
        string sortBy,
        string sortDir,
        int page,
        int pageSize);

    Task<TourDetailViewModel?> GetTourDetailAsync(int tourId);

    Task<TourFormViewModel?> GetForEditAsync(int tourId);

    Task<TourFormViewModel> PrepareCreateViewModelAsync();

    Task<bool> CheckTourCodeExistsAsync(string tourCode, int? excludeTourId = null);

    Task<bool> CreateTourAsync(TourFormViewModel vm, ModelStateDictionary modelState);

    Task<bool> UpdateTourAsync(int tourId, TourFormViewModel vm, ModelStateDictionary modelState);

    Task<(bool Success, string? ErrorMessage)> DeleteTourAsync(int tourId);

    Task<(bool Success, bool NewStatus)> ToggleStatusAsync(int tourId);
}
