namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the weekly notification scheduler
/// </summary>
public class WeeklyNotificationOptions
{
    public const string SectionName = "WeeklyNotifications";

    /// <summary>
    /// Whether the automatic weekly notification scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to send weekly notifications on Monday (format: "HH:mm")
    /// Default is 09:00
    /// </summary>
    public string ScheduledTime { get; set; } = "09:00";
}
