using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a Nerba order item (supply order line item tied to an event)
/// </summary>
public class NerbaOrder : BaseEntity
{
    [Required(ErrorMessage = "O item é obrigatório.")]
    [MaxLength(200)]
    public string Item { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Type { get; set; }

    [Required]
    public int Stock { get; set; }

    [Required]
    public decimal PricePerUnit { get; set; }

    public int? ReportId { get; set; }

    [ForeignKey(nameof(ReportId))]
    public virtual Report? Report { get; set; }

    [Required(ErrorMessage = "O evento é obrigatório.")]
    public int EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual Event Event { get; set; } = null!;
}
