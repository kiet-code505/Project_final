using System.ComponentModel.DataAnnotations;
using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class AdminUserFilter
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
    public Role? Role { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 6;
}

public class AdminUserSummary
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public Role Role { get; set; }
    public bool Status { get; set; }
    public bool IsEmailVerified { get; set; }
    public int BookingCount { get; set; }
    public string RoleLabel => Role switch { Role.ADMIN => "Quản trị viên", Role.TOUR_OWNER => "Nhà cung cấp", _ => "Khách hàng" };
}

public class AdminUserIndexViewModel
{
    public AdminUserFilter Filter { get; set; } = new();
    public List<AdminUserSummary> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / Filter.PageSize));
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int AdminUsers { get; set; }
}

public class AdminUserBookingRow
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = "";
    public DateTime BookingDate { get; set; }
    public string TourName { get; set; } = "";
    public string TourCode { get; set; } = "";
    public DateTime DepartureDate { get; set; }
    public int TotalPassengers { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
    public string StatusLabel => Status switch { BookingStatus.PAID => "Đã thanh toán", BookingStatus.CANCELLED => "Đã hủy", BookingStatus.PENDING_PAYMENT => "Chờ thanh toán", BookingStatus.EXPIRED => "Hết hạn thanh toán", _ => "Chờ xác nhận" };
}

public class AdminUserDetailsViewModel
{
    public required AdminUserSummary Account { get; set; }
    public List<AdminUserBookingRow> Bookings { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Account.BookingCount / 10d));
    public string? ReturnUrl { get; set; }
}

public class AdminUserStatusInput
{
    [Required(ErrorMessage = "Vui lòng chọn trạng thái tài khoản.")]
    public bool? Status { get; set; }
    public string? ReturnUrl { get; set; }
}

public class AdminUserStatusViewModel : AdminUserStatusInput
{
    public required AdminUserSummary Account { get; set; }
}
