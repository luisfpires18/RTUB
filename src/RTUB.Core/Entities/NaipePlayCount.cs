using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a play/view count record for naipe content
/// Tracks when and by whom a video/image was played/viewed
/// </summary>
public class NaipePlayCount : BaseEntity
{
    [Required]
    public int NaipeContentId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual NaipeContent? NaipeContent { get; set; }
    public virtual ApplicationUser? User { get; set; }
}
