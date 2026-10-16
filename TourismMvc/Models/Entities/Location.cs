using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

[Table("locations")]
public class Location : BaseEntity
{
    [Key, Column("locationid")] public int LocationID { get; set; }

    [Required(ErrorMessage = "Tên địa điểm là bắt buộc"), StringLength(150)]
    [Column("location_name")] public string LocationName { get; set; } = "";

    [Required, Column("slug")] public string Slug { get; set; } = "";
    [StringLength(20), Column("location_code")] public string? LocationCode { get; set; }
    [StringLength(10), Column("airport_code")] public string? AirportCode { get; set; }
    [StringLength(150), Column("airport_name")] public string? AirportName { get; set; }
    [Column("image_url")] public string? ImageURL { get; set; }
    [Column("region")] public Region Region { get; set; }
    [Column("description")] public string? Description { get; set; }
    [Column("status")] public bool Status { get; set; } = true;
}
