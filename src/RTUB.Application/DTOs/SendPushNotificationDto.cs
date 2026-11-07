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
    /// Also kept in each recipient's "Notificações RTUB" conversation (task 058, <see cref="Services.NotificationInbox"/>).
    /// False only where the push is already a copy of something in /messages (a chat message).
    /// </summary>
    public bool KeepInInbox { get; set; } = true;

    /// <summary>
    /// The catalogue key this push is filtered by (task 082, <see cref="Services.PushPreferenceCatalog"/>): a member who
    /// turned it off gets no device push, the inbox copy still. Null = not a preference (only the /profile test push).
    /// </summary>
    public string? PreferenceKey { get; set; }
}
