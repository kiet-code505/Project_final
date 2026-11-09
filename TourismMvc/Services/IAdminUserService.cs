using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IAdminUserService
{
    Task<AdminUserIndexViewModel> SearchAsync(AdminUserFilter filter);
    Task<AdminUserSummary?> GetAsync(int id);
    Task<AdminUserDetailsViewModel?> GetDetailsAsync(int id, int page);
    Task<(bool Success, string Message)> SetStatusAsync(int id, bool status, int actorId);
}
