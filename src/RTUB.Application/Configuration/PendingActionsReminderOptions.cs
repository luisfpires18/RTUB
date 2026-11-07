namespace RTUB.Application.Configuration;

/// <summary>
/// Task 078: the daily "As minhas ações pendentes" push (docs/react-pending-actions.md → Daily push).
/// </summary>
public class PendingActionsReminderOptions
{
    public const string SectionName = "PendingActionsReminder";

    public bool Enabled { get; set; } = true;

    /// <summary>Portugal time of day ("HH:mm"), summer and winter. Default 18:00.</summary>
    public string ScheduledTime { get; set; } = "18:00";

    /// <summary>
    /// How late a run may still start after its time (an app restart or a slow start): within this window the day's run
    /// happens once; past it the day is skipped. Never more than one day, so a long outage sends no backlog.
    /// </summary>
    public int CatchUpHours { get; set; } = 6;
}
