using System.ComponentModel.DataAnnotations;
using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class AdminBookingFilter
{
    [StringLength(150, ErrorMessage = "Từ khóa tối đa 150 ký tự.")]
    public string? Search { get; set; }
    [EnumDataType(typeof(BookingStatus))] public BookingStatus? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class AdminBookingListItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string TourName { get; set; } = "";
    public string TourCode { get; set; } = "";
    public DateTime DepartureDate { get; set; }
    public DateTime BookingDate { get; set; }
    public int TotalPassengers { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
}

public class AdminBookingIndexViewModel
{
    public AdminBookingFilter Filter { get; set; } = new();
    public List<AdminBookingListItem> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / Filter.PageSize));
    public int StatTotal { get; set; }
    public int StatPending { get; set; }
    public int StatPaid { get; set; }
    public int StatClosed { get; set; }
}

public class AdminBookingDetailsViewModel
{
    public required Booking Booking { get; set; }
    public string? AccountName { get; set; }
    public string? AccountEmail { get; set; }
    public string ReturnUrl { get; set; } = "/admin/bookings";
    public string? CancelBlockedReason { get; set; }
    public string? ConfirmBlockedReason { get; set; }
    public bool CanCancel => CancelBlockedReason == null;
    public bool CanConfirm => ConfirmBlockedReason == null;
}

public class AdminBookingActionInput
{
    [Required(ErrorMessage = "Thiếu trạng thái đơn cần xác nhận."), EnumDataType(typeof(BookingStatus))]
    public BookingStatus? ExpectedStatus { get; set; }
    [StringLength(2048)] public string? ReturnUrl { get; set; }
}

public class AdminBookingActionViewModel
{
    public required AdminBookingDetailsViewModel Details { get; set; }
    public bool ConfirmPayment { get; set; }
    public bool Allowed => ConfirmPayment ? Details.CanConfirm : Details.CanCancel;
    public string? BlockedReason => ConfirmPayment ? Details.ConfirmBlockedReason : Details.CancelBlockedReason;
}
