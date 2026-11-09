using TourismMvc.Models.Entities;

namespace TourismMvc.Models.ViewModels;

public class AdminPaymentViewModel
{
    public List<VnpayPayment> Items { get; set; } = new();
    public string? Search { get; set; }
    public VnpayPaymentStatus? Status { get; set; }
    public int Page { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / 20d));
    public int ReviewCount { get; set; }
}
