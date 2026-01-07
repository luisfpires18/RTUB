namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the member status update scheduler
/// </summary>
public class MemberStatusUpdateOptions
{
    public const string SectionName = "MemberStatusUpdate";

    /// <summary>
    /// Whether the automatic member status update scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to update member statuses (format: "HH:mm")
    /// Default is 21:00 (9 PM)
    /// </summary>
    public string ScheduledTime { get; set; } = "21:00";
    
    /// <summary>
    /// Whether to send push notifications for member status changes
    /// Set to false while testing/debugging the status update logic
    /// </summary>
    public bool PushNotificationsEnabled { get; set; } = false;
}
