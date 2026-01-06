using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a song video play count record
/// Tracks when and by whom a song video was played
/// </summary>
public class SongVideoPlayCount : BaseEntity
{
    [Required]
    public int SongVideoId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual SongVideo? SongVideo { get; set; }
    public virtual ApplicationUser? User { get; set; }
}
