using RTUB.Application.DTOs;

namespace RTUB.Controllers;

/// <summary>
/// Request model for sending to selected users via the API
/// </summary>
public class SendToSelectedRequest
{
    public IEnumerable<string> UserIds { get; set; } = Enumerable.Empty<string>();
    public SendPushNotificationDto Notification { get; set; } = new();
}
