using TourismMvc.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TourismMvc.Services;

public interface IDepartureService
{
    Task<DepartureIndexViewModel> GetPagedAsync(DepartureIndexViewModel filter);
    Task<DepartureDetailViewModel?> GetDetailAsync(int id);
    Task PrepareFormAsync(DepartureFormViewModel model);
    Task<DepartureFormViewModel?> GetForEditAsync(int id);
    Task<int?> SaveAsync(int? id, DepartureFormViewModel model, ModelStateDictionary errors);
    Task<DepartureCloneViewModel?> GetForCloneAsync(int id);
    Task<int?> CloneAsync(int id, DepartureCloneViewModel model, ModelStateDictionary errors);
    Task<(bool Success, string Message)> DeleteAsync(IEnumerable<int> ids);
}
