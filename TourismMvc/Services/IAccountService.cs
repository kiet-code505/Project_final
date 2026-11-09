using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IAccountService
{
    Task<(User? User, string? Error)> AuthenticateAsync(string email, string password, bool adminOnly);
    Task<string?> RegisterAsync(RegisterViewModel model);
}
