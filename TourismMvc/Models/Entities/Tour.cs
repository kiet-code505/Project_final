using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

[Table("tours")]
public class Tour : BaseEntity
{
    [Key, Column("tourid")] public int TourID { get; set; }

    [Required(ErrorMessage = "Mã tour là bắt buộc"), StringLength(50)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "Mã tour chỉ gồm chữ in hoa, số và dấu gạch ngang")]
    [Column("tour_code")] public string TourCode { get; set; } = "";

    [Required(ErrorMessage = "Tên tour là bắt buộc"), StringLength(255, MinimumLength = 10)]
    [Column("tour_name")] public string TourName { get; set; } = "";

    [Required(ErrorMessage = "Thời lượng là bắt buộc"), Column("duration")] public string Duration { get; set; } = "";
    [Required(ErrorMessage = "Phương tiện là bắt buộc"), Column("transportation")] public string Transportation { get; set; } = "";
    [Required(ErrorMessage = "Điểm tham quan là bắt buộc"), Column("attractions")] public string Attractions { get; set; } = "";
    [Column("meals")] public string? Meals { get; set; }
    [Column("ideal_time")] public string? IdealTime { get; set; }
    [Column("trip_transportation")] public string? TripTransportation { get; set; }
    [Column("suitable_customer")] public string? SuitableCustomer { get; set; }
    [Column("hotel")] public string? Hotel { get; set; }
    [Column("status")] public bool Status { get; set; } = true;

    [Column("start_location_id")] public int StartLocationId { get; set; }
    [Column("end_location_id")] public int EndLocationId { get; set; }
    public Location? StartLocation { get; set; }
    public Location? EndLocation { get; set; }

    public List<TourImage> Images { get; set; } = new();
    public List<ItineraryDay> ItineraryDays { get; set; } = new();
    public List<TourDeparture> Departures { get; set; } = new();
}

[Table("tour_images")]
public class TourImage
{
    [Key, Column("imageid")] public int ImageID { get; set; }
    [Required, Column("image_url")] public string ImageURL { get; set; } = "";
    [Column("is_main_image")] public bool IsMainImage { get; set; }
    [Column("sort_order")] public int? SortOrder { get; set; } = 0;
    [Column("tour_id")] public int TourId { get; set; }
    public Tour? Tour { get; set; }
}

[Table("itinerary_days")]
public class ItineraryDay
{
    [Key, Column("dayid")] public int DayID { get; set; }
    [Range(1, int.MaxValue), Column("day_number")] public int DayNumber { get; set; }
    [Required(ErrorMessage = "Tiêu đề ngày là bắt buộc"), Column("title")] public string Title { get; set; } = "";
    [Column("description")] public string? Description { get; set; }
    [Column("meals")] public string? Meals { get; set; }
    [Column("accommodation")] public string? Accommodation { get; set; }
    [Column("tour_id")] public int TourId { get; set; }
    public Tour? Tour { get; set; }
}
