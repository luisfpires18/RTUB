namespace RTUB.Application.Configuration;

public class CalotesNotificationOptions
{
    public const string SectionName = "CalotesNotifications";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to send calotes notifications (format: "HH:mm")
    /// Default is 10:30 (10:30 AM)
    /// </summary>
    public string ScheduledTime { get; set; } = "10:30";
}
