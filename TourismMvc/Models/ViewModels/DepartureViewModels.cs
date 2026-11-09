using TourismMvc.Models.Entities;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TourismMvc.Models.ViewModels;

public class DepartureListItem
{
    public int DepartureId { get; set; }
    public int TourId { get; set; }
    public string TourCode { get; set; } = "";
    public string TourName { get; set; } = "";
    public string Duration { get; set; } = "";
    public DateTime DepartureDate { get; set; }
    public bool Active { get; set; }
    public int AvailableSlots { get; set; }
    public int BookedSlots { get; set; }
    public decimal? LowestPrice { get; set; }
    public bool HasOutbound { get; set; }
    public bool HasInbound { get; set; }
}

public class DepartureIndexViewModel
{
    public List<DepartureListItem> Items { get; set; } = [];
    public string? Search { get; set; }
    public int? TourId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool? Status { get; set; }
    public bool NewestFirst { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public int ActiveCount { get; set; }
    public int AvailableSlots { get; set; }
    public int BookedSlots { get; set; }
}

public class DepartureDetailViewModel
{
    public required TourDeparture Departure { get; init; }
    public int BookedSlots { get; init; }
    public string? ReturnUrl { get; set; }
}

public class DepartureFormViewModel
{
    [BindNever] public int? Id { get; set; }
    public long? Revision { get; set; }
    public string? ReturnUrl { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tour.")]
    public int TourId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn ngày giờ khởi hành.")]
    public DateTime? DepartureDate { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập số chỗ trống."), Range(0, int.MaxValue, ErrorMessage = "Số chỗ trống không được âm.")]
    public int? AvailableSlots { get; set; }
    public bool Status { get; set; } = true;
    [Required(ErrorMessage = "Vui lòng nhập thông tin hướng dẫn viên."), StringLength(4000)]
    public string TourGuideInfo { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn mẫu chính sách.")]
    public int PolicyTemplateId { get; set; }
    public List<DeparturePriceInput> Pricings { get; set; } = Enum.GetValues<PassengerType>()
        .Select(t => new DeparturePriceInput { PassengerType = t, AgeDescription = DeparturePriceInput.DefaultAge(t) }).ToList();
    public List<DepartureTransportInput> Transports { get; set; } = Enum.GetValues<TransportType>()
        .Select(t => new DepartureTransportInput { Type = t }).ToList();
    [BindNever, ValidateNever] public List<SelectListItem> Tours { get; set; } = [];
    [BindNever, ValidateNever] public List<SelectListItem> Policies { get; set; } = [];
    [BindNever, ValidateNever] public List<SelectListItem> Airports { get; set; } = [];
}

public class DeparturePriceInput
{
    public PassengerType PassengerType { get; set; }
    [StringLength(255)] public string? AgeDescription { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "Giá gốc không được âm.")] public decimal? OriginalPrice { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "Giá bán không được âm.")] public decimal? SalePrice { get; set; }
    public static string Name(PassengerType type) => type switch
    {
        PassengerType.ADULT => "Người lớn", PassengerType.CHILD => "Trẻ em", PassengerType.INFANT => "Trẻ nhỏ",
        PassengerType.SINGLE_SUPPLEMENT => "Phụ thu phòng đơn", _ => "Không hợp lệ"
    };
    public static string DefaultAge(PassengerType type) => type switch
    {
        PassengerType.ADULT => "Từ 12 tuổi", PassengerType.CHILD => "Từ 5–11 tuổi", PassengerType.INFANT => "Dưới 5 tuổi", _ => "Phòng đơn"
    };
}

public class DepartureTransportInput
{
    public TransportType Type { get; set; }
    public bool Enabled { get; set; }
    public VehicleType VehicleType { get; set; }
    [StringLength(255)] public string? VehicleName { get; set; }
    [StringLength(255)] public string? TransportCode { get; set; }
    [StringLength(255)] public string? StartPoint { get; set; }
    [StringLength(255)] public string? EndPoint { get; set; }
    public DateTime? DepartTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
}

public class DepartureCloneViewModel
{
    [BindNever] public int Id { get; set; }
    [BindNever, ValidateNever] public string TourName { get; set; } = "";
    [BindNever] public DateTime OriginalDate { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn ngày giờ mới.")]
    public DateTime? NewDepartureDate { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập số chỗ."), Range(1, int.MaxValue, ErrorMessage = "Số chỗ phải lớn hơn 0.")]
    public int? AvailableSlots { get; set; }
    public string? ReturnUrl { get; set; }
}
