using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>A member the Owner lists for /trintuna (docs/react-trintuna.md). One list for the whole page.</summary>
public class TrintunaAccess : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser? User { get; set; }
}
