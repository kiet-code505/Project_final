using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IAdminBookingService
{
    Task<AdminBookingIndexViewModel> SearchAsync(AdminBookingFilter filter);
    Task<AdminBookingDetailsViewModel?> GetDetailsAsync(int id);
    Task<(bool Success, string Message)> CancelAsync(int id, BookingStatus expectedStatus);
    Task<(bool Success, string Message)> ConfirmPaymentAsync(int id, BookingStatus expectedStatus);
}
