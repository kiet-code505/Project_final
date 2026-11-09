using TourismMvc.Models.Entities;

namespace TourismMvc.Helpers;

public static class BookingStatusDisplay
{
    public static string Label(this BookingStatus status) => status switch
    {
        BookingStatus.PAID => "Đã thanh toán", BookingStatus.CANCELLED => "Đã hủy",
        BookingStatus.EXPIRED => "Hết hạn thanh toán", BookingStatus.PENDING_PAYMENT => "Chờ thanh toán", _ => "Chờ xác nhận"
    };
    public static string CssClass(this BookingStatus status) => status switch
    {
        BookingStatus.PAID => "booking-paid", BookingStatus.CANCELLED => "booking-cancelled",
        BookingStatus.EXPIRED => "booking-expired", _ => "booking-pending"
    };
}
