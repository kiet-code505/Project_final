using System.ComponentModel.DataAnnotations;
using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

// ── Branch ViewModels ────────────────────────────────────────────────────────

public class BranchListItem
{
    public required BranchContact Branch { get; init; }
    public int PolicyCount { get; init; }
}

public class BranchIndexViewModel
{
    public List<BranchListItem> Items { get; set; } = new();
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    // Thống kê
    public int StatTotal { get; set; }
    public int StatTotalPolicies { get; set; }
}

public class BranchFormViewModel
{
    public int? ContactID { get; set; }

    [Required(ErrorMessage = "Tên chi nhánh là bắt buộc")]
    [StringLength(150, ErrorMessage = "Tên chi nhánh không được quá 150 ký tự")]
    [Display(Name = "Tên chi nhánh")]
    public string BranchName { get; set; } = "";

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Số điện thoại không hợp lệ (10-11 chữ số)")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(100, ErrorMessage = "Email không được quá 100 ký tự")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Địa chỉ là bắt buộc")]
    [StringLength(255, ErrorMessage = "Địa chỉ không được quá 255 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = "";

    public bool IsEdit => ContactID.HasValue;
}

// ── Policy ViewModels ────────────────────────────────────────────────────────

public class PolicyListItem
{
    public required PolicyTemplate Policy { get; init; }
    public string? BranchName { get; init; }
    public int UsageCount { get; init; } // Số tour đang sử dụng
}

public class PolicyIndexViewModel
{
    public List<PolicyListItem> Items { get; set; } = new();
    public string? Search { get; set; }
    public int? ContactId { get; set; }
    public List<BranchContact> Branches { get; set; } = new();

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    // Thống kê
    public int StatTotal { get; set; }
    public int StatLinkedBranch { get; set; }
    public int StatTotalUsage { get; set; }
    public int StatUnused { get; set; }
}

public class PolicyFormViewModel
{
    public int? PolicyTemplateID { get; set; }

    [Required(ErrorMessage = "Tên mẫu chính sách là bắt buộc")]
    [StringLength(255, ErrorMessage = "Tên mẫu chính sách không quá 255 ký tự")]
    [Display(Name = "Tên mẫu chính sách")]
    public string TemplateName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn chi nhánh quản lý")]
    [Display(Name = "Chi nhánh quản lý")]
    public int? ContactId { get; set; }

    [Display(Name = "Giá tour bao gồm")]
    public string? TourPriceIncludes { get; set; }

    [Display(Name = "Giá tour không bao gồm")]
    public string? TourPriceExcludes { get; set; }

    [Display(Name = "Điều kiện thanh toán")]
    public string? PaymentConditions { get; set; }

    [Display(Name = "Quy định huỷ tour")]
    public string? CancellationRules { get; set; }

    public List<BranchContact> AvailableBranches { get; set; } = new();

    public bool IsEdit => PolicyTemplateID.HasValue;
}
