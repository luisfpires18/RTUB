using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a practice session for tracking user practice time and XP gamification
/// </summary>
public class PracticeSession : BaseEntity
{
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public InstrumentType InstrumentType { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int DurationMinutes { get; set; }

    public int? SongId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int XpAwarded { get; set; }

    // Navigation properties
    public ApplicationUser? User { get; set; }
    public Song? Song { get; set; }
}
