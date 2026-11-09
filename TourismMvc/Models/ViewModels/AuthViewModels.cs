using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TourismMvc.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(254)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), DataType(DataType.Password), StringLength(1024)]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public bool IsAdminLogin { get; set; }
}

public class RegisterViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên"), StringLength(100, MinimumLength = 3, ErrorMessage = "Họ tên phải từ 3 đến 100 ký tự")]
    [RegularExpression(@"^[\p{L}\p{M}\s]+$", ErrorMessage = "Họ tên chỉ được chứa chữ cái và khoảng trắng")]
    public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(254)]
    public string Email { get; set; } = "";
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Số điện thoại phải gồm 10–11 chữ số")]
    public string? Phone { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), StringLength(72, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 72 ký tự")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mật khẩu cần có chữ hoa, chữ thường và chữ số")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu"), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận chưa khớp"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Password) && Encoding.UTF8.GetByteCount(Password) > 72)
            yield return new ValidationResult("Mật khẩu quá dài. Vui lòng dùng tối đa 72 byte UTF-8.", [nameof(Password)]);
    }
}
