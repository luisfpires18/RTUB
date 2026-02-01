using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks user confirmations/refusals of published meeting ATAs
/// Only users who participated (WillAttend = true) can confirm/refuse
/// </summary>
public class MeetingAtaConfirmation : BaseEntity
{
    [Required]
    public int MeetingAtaId { get; set; }

    public MeetingAta MeetingAta { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// True if user confirms the ATA, false if they refuse
    /// Null means no response yet
    /// </summary>
    public bool? IsConfirmed { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    // Private constructor for EF Core
    private MeetingAtaConfirmation() { }

    public static MeetingAtaConfirmation Create(int meetingAtaId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        if (meetingAtaId <= 0)
            throw new ArgumentException("Meeting ATA ID must be greater than 0", nameof(meetingAtaId));

        return new MeetingAtaConfirmation
        {
            MeetingAtaId = meetingAtaId,
            UserId = userId
        };
    }

    public void Confirm(string? notes = null)
    {
        IsConfirmed = true;
        Notes = notes;
        ConfirmedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Refuse(string? notes = null)
    {
        IsConfirmed = false;
        Notes = notes;
        ConfirmedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
