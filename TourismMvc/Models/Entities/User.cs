using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

[Table("users")]
public class User
{
    [Key, Column("userid")] public int UserID { get; set; }
    [Required, StringLength(100), Column("full_name")] public string FullName { get; set; } = "";
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "SĐT phải gồm 10-11 chữ số"), Column("phone")] public string? Phone { get; set; }
    [Required, EmailAddress, Column("email")] public string Email { get; set; } = "";
    [Required, Column("password")] public string Password { get; set; } = "";   // BCrypt ($2a$) từ Spring, dùng chung được
    [Column("status")] public bool Status { get; set; } = true;                 // false = bị khoá
    [Column("role")] public Role Role { get; set; } = Role.CUSTOMER;
    [Column("is_email_verified")] public bool IsEmailVerified { get; set; } = true; // cột NOT NULL, luôn true

    public List<Booking> Bookings { get; set; } = new();
}
