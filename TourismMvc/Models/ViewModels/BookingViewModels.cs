using System.ComponentModel.DataAnnotations;
using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class BookingInputModel
{
    [Required, StringLength(50)] public string TourCode { get; set; } = "";
    [Range(1, int.MaxValue)] public int DepartureId { get; set; }
    [Required, StringLength(2048)] public string SubmissionToken { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập họ tên liên hệ"), StringLength(100)] public string ContactFullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(254)] public string ContactEmail { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), RegularExpression(@"^\d{10,11}$", ErrorMessage = "Số điện thoại phải gồm 10–11 chữ số")] public string ContactPhone { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ liên hệ"), StringLength(500)] public string ContactAddress { get; set; } = "";
    [StringLength(2000, ErrorMessage = "Ghi chú tối đa 2000 ký tự")] public string? CustomerNote { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Vui lòng xác nhận đã đọc chính sách của chuyến đi")] public bool AgreeToPolicy { get; set; }
    [MinLength(1, ErrorMessage = "Vui lòng thêm hành khách"), MaxLength(50, ErrorMessage = "Mỗi yêu cầu tối đa 50 hành khách")]
    public List<BookingPassengerInput> Passengers { get; set; } = [new()];
}

public class BookingPassengerInput
{
    [Required(ErrorMessage = "Vui lòng nhập tên hành khách"), StringLength(100)] public string FullName { get; set; } = "";
    [Required, RegularExpression("^(Nam|Nữ|Khác)$", ErrorMessage = "Giới tính không hợp lệ")] public string Gender { get; set; } = "Nam";
    [Required(ErrorMessage = "Vui lòng nhập ngày sinh")] public DateOnly? DateOfBirth { get; set; }
    [Required, RegularExpression("^(ADULT|CHILD|INFANT)$", ErrorMessage = "Loại hành khách không hợp lệ")] public string PassengerType { get; set; } = "ADULT";
}

public class BookingTourInfo
{
    public int TourId { get; init; }
    public string TourCode { get; init; } = "";
    public string TourName { get; init; } = "";
    public string Duration { get; init; } = "";
    public string StartLocation { get; init; } = "";
    public string EndLocation { get; init; } = "";
    public string? ImageUrl { get; init; }
    public required TourDeparture Departure { get; init; }
    public List<DeparturePricing> Prices { get; init; } = [];
    public string QuoteStamp { get; init; } = "";
    public bool CanBook { get; init; }
}

public class BookingOrderViewModel
{
    public required BookingTourInfo Info { get; init; }
    public BookingInputModel Input { get; set; } = new();
    public bool ContactPrefilled { get; set; }
    public decimal EstimatedTotal => Input.Passengers.Sum(p => Info.Prices.FirstOrDefault(price => price.PassengerType.ToString() == p.PassengerType)?.EffectivePrice ?? 0);
}

public class BookingConfirmationViewModel
{
    public required Booking Booking { get; init; }
    public BookingPaymentState Payment { get; init; } = new();
}

public record BookingSubmission(string Code, int DepartureId, string QuoteStamp, int? UserId, string BrowserId);
