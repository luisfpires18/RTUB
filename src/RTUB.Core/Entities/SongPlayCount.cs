using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a song play count record
/// Tracks when and by whom a song was played
/// </summary>
public class SongPlayCount : BaseEntity
{
    [Required]
    public int SongId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Song? Song { get; set; }
    public virtual ApplicationUser? User { get; set; }
}
