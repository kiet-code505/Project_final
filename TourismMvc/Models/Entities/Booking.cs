using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

[Table("bookings")]
public class Booking
{
    [Key, Column("bookingid")] public int BookingID { get; set; }
    [Required, StringLength(20), Column("booking_code")] public string BookingCode { get; set; } = "BK" + Guid.NewGuid().ToString("N")[..8];
    [Column("booking_date")] public DateTime BookingDate { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress, Column("contact_email")] public string ContactEmail { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), Column("contact_full_name")] public string ContactFullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập SĐT"), Column("contact_phone")] public string ContactPhone { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ"), Column("contact_address")] public string ContactAddress { get; set; } = "";
    [Column("customer_note")] public string? CustomerNote { get; set; }

    [Column("total_passengers")] public int TotalPassengers { get; set; }
    [Column("subtotal_price")] public decimal SubtotalPrice { get; set; }
    [Column("total_price")] public decimal TotalPrice { get; set; }
    [Column("booking_status")] public BookingStatus BookingStatus { get; set; } = BookingStatus.PENDING_CONFIRMATION;

    [Column("departue_id")] public int DepartureId { get; set; }   // tên cột gõ sai từ bản Java, giữ nguyên
    [Column("user_id")] public int? UserId { get; set; }
    public TourDeparture? TourDeparture { get; set; }
    public User? User { get; set; }

    public List<BookingPassenger> Passengers { get; set; } = new();
}

[Table("booking_passengers")]
public class BookingPassenger
{
    [Key, Column("booking_passengerid")] public int BookingPassengerID { get; set; }
    [Required(ErrorMessage = "Nhập tên hành khách"), Column("full_name")] public string FullName { get; set; } = "";
    [Required, Column("gender")] public string Gender { get; set; } = "Nam";
    [Column("date_of_birth")] public DateOnly DateOfBirth { get; set; }
    [Required, RegularExpression("^(ADULT|CHILD|INFANT)$"), Column("passenger_type")] public string PassengerType { get; set; } = "ADULT";
    [Column("base_price")] public decimal BasePrice { get; set; }
    [Column("booking_id")] public int BookingId { get; set; }
    public Booking? Booking { get; set; }
}
