using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an MBWAY transfer transaction record
/// </summary>
public class MbwayTransfer : BaseEntity
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "O montante deve ser superior a 0€.")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Free-text name when the sender is not a tuna member.
    /// Mutually exclusive with MemberUserId.
    /// </summary>
    [MaxLength(200)]
    public string? PersonName { get; set; }

    /// <summary>
    /// The member who made the transfer (null when PersonName is used instead).
    /// </summary>
    public string? MemberUserId { get; set; }

    [ForeignKey(nameof(MemberUserId))]
    public virtual ApplicationUser? Member { get; set; }

    /// <summary>
    /// Phone number associated with the transfer (for reference)
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; set; }

    public int? FiscalYearId { get; set; }

    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear? FiscalYear { get; set; }
}
