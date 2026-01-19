using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a reminder for a logistics card.
/// Sends push notifications to target users at specified frequency.
/// </summary>
public class LogisticsCardReminder : BaseEntity
{
    [Required]
    public int CardId { get; set; }

    [Required]
    public ReminderFrequency Frequency { get; set; } = ReminderFrequency.OneTime;

    /// <summary>
    /// Comma-separated list of user IDs to receive the reminder
    /// </summary>
    [Required]
    public string TargetUserIds { get; set; } = string.Empty;

    /// <summary>
    /// Next scheduled time for the reminder
    /// </summary>
    [Required]
    public DateTime NextReminderDate { get; set; }

    /// <summary>
    /// Indicates if the reminder is still active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time the reminder was sent
    /// </summary>
    public DateTime? LastSentAt { get; set; }

    // Navigation properties
    public virtual LogisticsCard Card { get; set; } = null!;

    // Private constructor for EF Core
    public LogisticsCardReminder() { }

    // Factory method
    public static LogisticsCardReminder Create(int cardId, ReminderFrequency frequency, string targetUserIds, DateTime nextReminderDate)
    {
        if (string.IsNullOrWhiteSpace(targetUserIds))
            throw new ArgumentException("Pelo menos um utilizador deve ser selecionado", nameof(targetUserIds));

        return new LogisticsCardReminder
        {
            CardId = cardId,
            Frequency = frequency,
            TargetUserIds = targetUserIds,
            NextReminderDate = nextReminderDate
        };
    }

    // Business methods
    public void MarkAsSent()
    {
        LastSentAt = DateTime.UtcNow;

        // Update next reminder date based on frequency
        switch (Frequency)
        {
            case ReminderFrequency.Daily:
                NextReminderDate = NextReminderDate.AddDays(1);
                break;
            case ReminderFrequency.Weekly:
                NextReminderDate = NextReminderDate.AddDays(7);
                break;
            case ReminderFrequency.OneTime:
                IsActive = false;
                break;
        }
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void UpdateTargetUsers(string targetUserIds)
    {
        if (string.IsNullOrWhiteSpace(targetUserIds))
            throw new ArgumentException("Pelo menos um utilizador deve ser selecionado", nameof(targetUserIds));

        TargetUserIds = targetUserIds;
    }
}
