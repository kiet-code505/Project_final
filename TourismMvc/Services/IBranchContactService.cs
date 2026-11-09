using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IBranchContactService
{
    Task<BranchIndexViewModel> GetPagedBranchesAsync(string? search, int page, int pageSize);
    Task<BranchFormViewModel?> GetForEditAsync(int id);
    Task<List<BranchContact>> GetAllActiveBranchesAsync();
    Task<bool> CreateAsync(BranchFormViewModel vm, ModelStateDictionary modelState);
    Task<bool> UpdateAsync(int id, BranchFormViewModel vm, ModelStateDictionary modelState);
    Task<(bool Success, string? ErrorMessage)> DeleteAsync(int id);
}
