namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for daily activity reminder notifications
/// (events, rehearsals, and meetings).
/// </summary>
public class ActivityReminderOptions
{
    public const string SectionName = "ActivityReminders";

    /// <summary>
    /// Whether automatic activity reminders are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time of day to send reminders (format: "HH:mm").
    /// Default is 11:30 (11:30 AM).
    /// </summary>
    public string ScheduledTime { get; set; } = "11:30";
}

