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
}
