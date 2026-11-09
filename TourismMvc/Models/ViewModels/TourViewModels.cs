using System.ComponentModel.DataAnnotations;
using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

// ── Item hiển thị trong danh sách tour ───────────────────────────────────────
public class TourListItem
{
    public required Tour Tour { get; init; }
    public string? MainImageUrl { get; init; }
    public int ImageCount { get; init; }
    public int ItineraryCount { get; init; }
    public int DepartureCount { get; init; }
}

// ── ViewModel cho trang danh sách Tour (Index) ──────────────────────────────
public class TourIndexViewModel
{
    public List<TourListItem> Items { get; set; } = new();

    // Lọc, tìm kiếm, sắp xếp & phân trang
    public string? Search { get; set; }
    public int? StartLocationId { get; set; }
    public int? EndLocationId { get; set; }
    public bool? Status { get; set; }
    public string SortBy { get; set; } = "updatedAt";
    public string SortDir { get; set; } = "DESC";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    // Thống kê nhanh
    public int StatTotal { get; set; }
    public int StatActive { get; set; }
    public int StatInactive { get; set; }
    public int StatWithDepartures { get; set; }

    // Danh sách địa điểm phục vụ dropdown lọc
    public List<LocationOptionViewModel> LocationOptions { get; set; } = new();
}

public class LocationOptionViewModel
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
}

// ── ViewModel xem chi tiết Tour (Modal & Chi tiết) ──────────────────────────
public class TourDetailViewModel
{
    public int TourID { get; set; }
    public string TourCode { get; set; } = "";
    public string TourName { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Transportation { get; set; } = "";
    public string Attractions { get; set; } = "";
    public string? Meals { get; set; }
    public string? IdealTime { get; set; }
    public string? TripTransportation { get; set; }
    public string? SuitableCustomer { get; set; }
    public string? Hotel { get; set; }
    public bool Status { get; set; }

    public int StartLocationId { get; set; }
    public string StartLocationName { get; set; } = "";
    public int EndLocationId { get; set; }
    public string EndLocationName { get; set; } = "";

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? MainImageUrl { get; set; }
    public List<TourImageItemViewModel> Images { get; set; } = new();
    public List<TourItineraryItemViewModel> ItineraryDays { get; set; } = new();
    public int DepartureCount { get; set; }
}

public class TourImageItemViewModel
{
    public int ImageID { get; set; }
    public string ImageURL { get; set; } = "";
    public bool IsMainImage { get; set; }
    public int? SortOrder { get; set; }
}

public class TourItineraryItemViewModel
{
    public int DayID { get; set; }
    public int DayNumber { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Meals { get; set; }
    public string? Accommodation { get; set; }
}

// ── ViewModel cho Form Tạo / Sửa Tour ─────────────────────────────────────────
public class TourFormViewModel
{
    public int? TourID { get; set; }

    [Required(ErrorMessage = "Mã tour là bắt buộc")]
    [StringLength(50, ErrorMessage = "Mã tour tối đa 50 ký tự")]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "Mã tour chỉ gồm chữ in hoa, số và dấu gạch ngang (ví dụ: TOUR-HN-DN-01)")]
    [Display(Name = "Mã tour")]
    public string TourCode { get; set; } = "";

    [Required(ErrorMessage = "Tên tour là bắt buộc")]
    [StringLength(255, MinimumLength = 5, ErrorMessage = "Tên tour phải từ 5 đến 255 ký tự")]
    [Display(Name = "Tên tour")]
    public string TourName { get; set; } = "";

    [Required(ErrorMessage = "Thời lượng tour là bắt buộc")]
    [StringLength(100, ErrorMessage = "Thời lượng tối đa 100 ký tự")]
    [Display(Name = "Thời lượng")]
    public string Duration { get; set; } = "";

    [Required(ErrorMessage = "Phương tiện di chuyển là bắt buộc")]
    [StringLength(150, ErrorMessage = "Phương tiện tối đa 150 ký tự")]
    [Display(Name = "Phương tiện")]
    public string Transportation { get; set; } = "";

    [Required(ErrorMessage = "Điểm khởi hành là bắt buộc")]
    [Display(Name = "Điểm khởi hành")]
    public int? StartLocationId { get; set; }

    [Required(ErrorMessage = "Điểm đến là bắt buộc")]
    [Display(Name = "Điểm đến")]
    public int? EndLocationId { get; set; }

    [Required(ErrorMessage = "Điểm tham quan là bắt buộc")]
    [Display(Name = "Điểm tham quan chính")]
    public string Attractions { get; set; } = "";

    [Display(Name = "Ẩm thực")]
    public string? Meals { get; set; }

    [Display(Name = "Thời gian lý tưởng")]
    public string? IdealTime { get; set; }

    [Display(Name = "Phương tiện trong chuyến đi")]
    public string? TripTransportation { get; set; }

    [Display(Name = "Đối tượng thích hợp")]
    public string? SuitableCustomer { get; set; }

    [Display(Name = "Khách sạn / Lưu trú")]
    public string? Hotel { get; set; }

    [Display(Name = "Trạng thái hoạt động")]
    public bool Status { get; set; } = true;

    // Danh sách ngày trong lịch trình
    public List<TourItineraryItemInput> ItineraryDays { get; set; } = new();

    // Danh sách hình ảnh
    public List<TourImageItemInput> Images { get; set; } = new();

    // Dropdowns
    public List<LocationOptionViewModel> LocationOptions { get; set; } = new();

    public bool IsEdit => TourID.HasValue && TourID.Value > 0;
}

public class TourItineraryItemInput
{
    public int? DayID { get; set; }

    [Required(ErrorMessage = "Thứ tự ngày là bắt buộc")]
    [Range(1, 100, ErrorMessage = "Thứ tự ngày từ 1 đến 100")]
    public int DayNumber { get; set; }

    [Required(ErrorMessage = "Tiêu đề ngày là bắt buộc")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự")]
    public string Title { get; set; } = "";

    public string? Description { get; set; }
    public string? Meals { get; set; }
    public string? Accommodation { get; set; }
}

public class TourImageItemInput
{
    public int? ImageID { get; set; }

    [Required(ErrorMessage = "Đường dẫn ảnh là bắt buộc")]
    public string ImageURL { get; set; } = "";

    public bool IsMainImage { get; set; }
    public int SortOrder { get; set; }
}
