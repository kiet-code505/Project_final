using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IPolicyTemplateService
{
    Task<PolicyIndexViewModel> GetPagedPoliciesAsync(string? search, int? contactId, int page, int pageSize);
    Task<PolicyFormViewModel?> GetForEditAsync(int id);
    Task<bool> CreateAsync(PolicyFormViewModel vm, ModelStateDictionary modelState);
    Task<bool> UpdateAsync(int id, PolicyFormViewModel vm, ModelStateDictionary modelState);
    Task<(bool Success, string? ErrorMessage)> DeleteAsync(int id);
    Task<List<PolicyTemplate>> GetAllPoliciesSimpleAsync();
}
