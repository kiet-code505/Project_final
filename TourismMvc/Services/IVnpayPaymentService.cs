using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface IVnpayPaymentService
{
    Task<BookingPaymentState> GetStateAsync(int bookingId);
    Task<PaymentStartResult> StartAsync(int bookingId, string ipAddress);
    Task<VnpayNotificationResult> HandleIpnAsync(IQueryCollection query);
    Task<string?> HandleReturnAsync(IQueryCollection query);
    Task RefreshAsync(string bookingCode, CancellationToken cancellationToken = default);
    Task ExpireAsync(string bookingCode);
}
