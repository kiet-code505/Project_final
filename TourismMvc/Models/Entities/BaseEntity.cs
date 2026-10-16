using System.ComponentModel.DataAnnotations.Schema;

namespace TourismMvc.Models.Entities;

public abstract class BaseEntity
{
    [Column("created_at")] public DateTime? CreatedAt { get; set; }
    [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
    [Column("created_by")] public string? CreatedBy { get; set; }
    [Column("updated_by")] public string? UpdatedBy { get; set; }
    [Column("is_deleted")] public bool? IsDeleted { get; set; } = false;
}
