using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

public enum VnpayPaymentStatus { PENDING, SUCCEEDED, FAILED, EXPIRED, REVIEW_REQUIRED }

[Table("mvc_vnpay_payments")]
public class VnpayPayment
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("booking_id")] public int BookingId { get; set; }
    [Required, MaxLength(40), Column("transaction_ref")] public string TransactionRef { get; set; } = "";
    [Required, MaxLength(32), Column("merchant_code")] public string MerchantCode { get; set; } = "";
    [Column("amount")] public decimal Amount { get; set; }
    [Column("status")] public VnpayPaymentStatus Status { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("last_checked_at")] public DateTime? LastCheckedAt { get; set; }
    [Column("settled_at")] public DateTime? SettledAt { get; set; }
    [MaxLength(100), Column("provider_transaction_no")] public string? ProviderTransactionNo { get; set; }
    [MaxLength(50), Column("bank_code")] public string? BankCode { get; set; }
    [MaxLength(10), Column("response_code")] public string? ResponseCode { get; set; }
    [MaxLength(10), Column("transaction_status")] public string? TransactionStatus { get; set; }
    public Booking? Booking { get; set; }
    [NotMapped] public string StatusLabel => Status switch
    {
        VnpayPaymentStatus.SUCCEEDED => "Thành công", VnpayPaymentStatus.FAILED => "Không thành công",
        VnpayPaymentStatus.EXPIRED => "Hết hạn", VnpayPaymentStatus.REVIEW_REQUIRED => "Cần đối soát", _ => "Chờ thanh toán"
    };
}
