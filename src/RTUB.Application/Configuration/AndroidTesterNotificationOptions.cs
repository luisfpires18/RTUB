namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for Android tester reminder notifications.
/// Sends reminders to Android testers at specific times during the day to test the app.
/// </summary>
public class AndroidTesterNotificationOptions
{
    public const string SectionName = "AndroidTesterNotifications";

    /// <summary>
    /// Whether automatic Android tester reminder notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Start date of the Android testing campaign.
    /// Notifications will only be sent on or after this date.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date of the Android testing campaign.
    /// Notifications will only be sent on or before this date.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// List of times during the day to send notifications (format: "HH:mm").
    /// If not configured, defaults to: 09:00, 12:00, 15:00, 18:00, 21:00 (UTC).
    /// </summary>
    public List<string> NotificationTimes { get; set; } = new();
}
