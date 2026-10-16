using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

[Table("branch_contacts")]
public class BranchContact : BaseEntity
{
    [Key, Column("contactid")] public int ContactID { get; set; }
    [Required(ErrorMessage = "Tên chi nhánh là bắt buộc"), Column("branch_name")] public string BranchName { get; set; } = "";
    [Required(ErrorMessage = "SĐT là bắt buộc"), Column("phone")] public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Email là bắt buộc"), EmailAddress, Column("email")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Địa chỉ là bắt buộc"), Column("address")] public string Address { get; set; } = "";
    public List<PolicyTemplate> Policies { get; set; } = new();
}

[Table("policy_templates")]
public class PolicyTemplate : BaseEntity
{
    [Key, Column("policy_templateid")] public int PolicyTemplateID { get; set; }
    [Required(ErrorMessage = "Tên mẫu chính sách là bắt buộc"), StringLength(255)]
    [Column("template_name")] public string TemplateName { get; set; } = "";

    [Column("tour_price_includes")] public string? TourPriceIncludes { get; set; }   // Giá tour bao gồm
    [Column("tour_price_excludes")] public string? TourPriceExcludes { get; set; }   // Giá tour không bao gồm
    [Column("payment_conditions")] public string? PaymentConditions { get; set; }    // Điều kiện thanh toán
    [Column("regular_day_cancellation_rules")] public string? CancellationRules { get; set; } // Quy định huỷ tour

    [Column("contact_id")] public int? ContactId { get; set; }   // Chi nhánh phụ trách
    public BranchContact? Contact { get; set; }
    public List<TourDeparture> TourDepartures { get; set; } = new();
}
