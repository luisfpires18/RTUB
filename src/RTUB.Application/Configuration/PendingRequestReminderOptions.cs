namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the pending request reminder scheduler
/// </summary>
public class PendingRequestReminderOptions
{
    public const string SectionName = "PendingRequestReminder";

    /// <summary>
    /// Whether the automatic pending request reminder scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to send pending request reminders (format: "HH:mm")
    /// Default is 10:00 (10 AM)
    /// </summary>
    public string ScheduledTime { get; set; } = "10:00";
}
