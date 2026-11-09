using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IBookingOrderService
{
    Task<BookingTourInfo?> GetInfoAsync(string tourCode, int departureId);
    Task<bool> PrefillAsync(BookingInputModel input, int userId);
    Task<Booking?> CreateAsync(BookingInputModel input, BookingSubmission submission, ModelStateDictionary modelState);
    Task<Booking?> GetConfirmationAsync(string code);
}
