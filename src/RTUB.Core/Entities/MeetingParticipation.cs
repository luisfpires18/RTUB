using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks member participation in meetings
/// Similar to Enrollment but for meetings
/// </summary>
public class MeetingParticipation : BaseEntity
{
    [Required]
    public int MeetingId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool WillAttend { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime ParticipatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Meeting? Meeting { get; set; }
    public virtual ApplicationUser? User { get; set; }

    // Private constructor for EF Core
    public MeetingParticipation() { }

    // Factory method - ensures valid entity creation
    public static MeetingParticipation Create(int meetingId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        if (meetingId <= 0)
            throw new ArgumentException("O ID da reunião deve ser maior que 0", nameof(meetingId));

        return new MeetingParticipation
        {
            MeetingId = meetingId,
            UserId = userId,
            ParticipatedAt = DateTime.UtcNow
        };
    }

    // Business methods
    public void UpdateAttendance(bool willAttend)
    {
        WillAttend = willAttend;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }
}
