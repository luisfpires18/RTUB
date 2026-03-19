namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for sending push notifications
/// </summary>
public class SendPushNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public string? Tag { get; set; }
    /// <summary>
    /// The recipient's total unread message count at the time of sending.
    /// Included in the push payload so the service worker can set an accurate app badge
    /// even when the app is closed (avoids using the stale OS notification count).
    /// </summary>
    public int? UnreadCount { get; set; }
}
