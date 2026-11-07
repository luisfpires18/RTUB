using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// A reminder for a logistics card, delivered by <c>LogisticsReminderDelivery</c> (task 069) as a push plus the
/// "Notificações RTUB" inbox copy. <see cref="NextReminderDate"/> is Portugal wall-clock time (Europe/Lisbon): a daily
/// reminder at 10:00 stays at 10:00 across the DST changes.
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

    /// <summary>Who created it (task 069); null on reminders saved before 069.</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>A member's own reminder (069): only for themselves, only while they work on the card.</summary>
    public bool Personal { get; set; }

    /// <summary>
    /// When delivery took this reminder on (UTC). Null = saved before 069 and never sent: the first delivery run
    /// activates it without replaying the past (see LogisticsReminderDelivery.ActivateLegacyAsync).
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>A delivery run holds the reminder until then (UTC), so two runs never send the same occurrence.</summary>
    public DateTime? ClaimedUntil { get; set; }

    /// <summary>Failed attempts at the current occurrence (bounded retry).</summary>
    public int Attempts { get; set; }

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

    /// <summary>
    /// Past the current occurrence: a one-time reminder stops; a daily / weekly one moves, in Portugal wall-clock time,
    /// to its first occurrence after <paramref name="nowLocal"/> (missed ones are skipped, never replayed).
    /// </summary>
    public void Advance(DateTime nowLocal)
    {
        Attempts = 0;
        ClaimedUntil = null;
        if (Frequency == ReminderFrequency.OneTime)
        {
            IsActive = false;
            return;
        }

        var step = Frequency == ReminderFrequency.Weekly ? 7 : 1;
        do
        {
            NextReminderDate = NextReminderDate.AddDays(step);
        }
        while (NextReminderDate <= nowLocal);
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
