using System.ComponentModel.DataAnnotations;
using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

/// <summary>Một dòng trong bảng — tương đương LocationResponse bên Spring.</summary>
public class LocationListItem
{
    public required Location Location { get; init; }
    public long ToursAsStartPoint { get; init; }
    public long ToursAsEndPoint { get; init; }
}

public class LocationIndexViewModel
{
    public List<LocationListItem> Items { get; set; } = new();

    // Bộ lọc / sắp xếp / phân trang (query string)
    public string? Search { get; set; }
    public Region? Region { get; set; }
    public string SortBy { get; set; } = "updatedAt";
    public string SortDir { get; set; } = "DESC";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    // Thống kê toàn bộ DB
    public int StatTotal { get; set; }
    public int StatNorth { get; set; }
    public int StatCentral { get; set; }
    public int StatSouth { get; set; }
    public int StatWithAirport { get; set; }
}

/// <summary>Form tạo/sửa — tương đương LocationRequest bên Spring.</summary>
public class LocationFormViewModel
{
    public int? LocationID { get; set; }

    [Required(ErrorMessage = "Tên địa điểm không được để trống")]
    [StringLength(150, ErrorMessage = "Tên địa điểm không được quá 150 ký tự")]
    [Display(Name = "Tên địa điểm")]
    public string Name { get; set; } = "";

    [StringLength(255, ErrorMessage = "Slug không được quá 255 ký tự")]
    [RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Slug chỉ gồm chữ thường, số và dấu gạch ngang")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Vùng miền không được để trống")]
    [Display(Name = "Khu vực")]
    public Region? Region { get; set; } = Entities.Region.NORTH;

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [StringLength(10, ErrorMessage = "Mã sân bay không được quá 10 ký tự")]
    [Display(Name = "Mã sân bay")]
    public string? AirportCode { get; set; }

    [StringLength(150, ErrorMessage = "Tên sân bay không được quá 150 ký tự")]
    [Display(Name = "Tên sân bay")]
    public string? AirportName { get; set; }

    /// <summary>Ảnh hiện tại (URL Cloudinary cũ hoặc /uploads/...).</summary>
    public string? ImageURL { get; set; }

    [Display(Name = "Ảnh")]
    public IFormFile? ImageFile { get; set; }

    public bool IsEdit => LocationID.HasValue;
}
