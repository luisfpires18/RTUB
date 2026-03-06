using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a Nerba order (supply order)
/// </summary>
public class NerbaOrder : BaseEntity
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    [MaxLength(200)]
    public string Item { get; set; } = string.Empty;

    [Required]
    public int Stock { get; set; }

    [Required]
    public decimal PricePerUnit { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? ReportId { get; set; }

    [ForeignKey(nameof(ReportId))]
    public virtual Report? Report { get; set; }
}
