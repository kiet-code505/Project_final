using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

[Table("tour_departures")]
public class TourDeparture : BaseEntity
{
    [Key, Column("departureid")] public int DepartureID { get; set; }
    [Required(ErrorMessage = "Ngày khởi hành là bắt buộc"), Column("departure_date")] public DateTime DepartureDate { get; set; }
    [Column("status")] public bool? Status { get; set; } = true;
    [Range(1, int.MaxValue, ErrorMessage = "Số chỗ tối thiểu là 1"), Column("available_slots")] public int AvailableSlots { get; set; }
    [Required(ErrorMessage = "Thông tin HDV là bắt buộc"), Column("tour_guide_info")] public string TourGuideInfo { get; set; } = "";

    [Column("policy_template_id")] public int PolicyTemplateId { get; set; }
    [Column("tour_id")] public int TourId { get; set; }
    public PolicyTemplate? PolicyTemplate { get; set; }
    public Tour? Tour { get; set; }

    public List<DeparturePricing> Pricings { get; set; } = new();
    public List<DepartureTransport> Transports { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}

[Table("departure_pricing")]
public class DeparturePricing
{
    [Key, Column("pricingid")] public int PricingID { get; set; }
    [Column("passenger_type")] public PassengerType PassengerType { get; set; }
    [Required, Column("age_description")] public string AgeDescription { get; set; } = "";
    [Range(0, double.MaxValue), Column("sale_price")] public decimal? SalePrice { get; set; }
    [Range(0, double.MaxValue), Column("original_price")] public decimal OriginalPrice { get; set; }
    [Column("departure_id")] public int DepartureId { get; set; }
    public TourDeparture? TourDeparture { get; set; }

    [NotMapped] public decimal EffectivePrice => SalePrice ?? OriginalPrice;
}

[Table("departure_transports")]
public class DepartureTransport
{
    [Key, Column("transportid")] public int TransportID { get; set; }
    [Column("vehicle_type")] public VehicleType VehicleType { get; set; }   // Java: vehicleTyle
    [Column("vehicle_name")] public string? VehicleName { get; set; }
    [Column("transport_code")] public string? TransportCode { get; set; }
    [Column("start_point")] public string? StartPoint { get; set; }
    [Column("end_point")] public string? EndPoint { get; set; }
    [Column("depart_time")] public DateTime? DepartTime { get; set; }
    [Column("arrival_time")] public DateTime? ArrivalTime { get; set; }
    [Column("type")] public TransportType Type { get; set; }
    [Column("departure_id")] public int DepartureId { get; set; }
    public TourDeparture? TourDeparture { get; set; }
}
