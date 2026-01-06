using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an event video play count record
/// Tracks when and by whom an event video was played
/// </summary>
public class EventVideoPlayCount : BaseEntity
{
    [Required]
    public int EventVideoId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual EventVideo? EventVideo { get; set; }
    public virtual ApplicationUser? User { get; set; }
}
