namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for push subscription requests from the client
/// </summary>
public class PushSubscriptionDto
{
    public string Endpoint { get; set; } = string.Empty;
    public PushKeysDto Keys { get; set; } = new();
    public DateTime? ExpirationTime { get; set; }
}

/// <summary>
/// DTO for push subscription keys
/// </summary>
public class PushKeysDto
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}

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

/// <summary>
/// DTO for push feature status response
/// </summary>
public class PushStatusDto
{
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public string? VapidPublicKey { get; set; }
}

/// <summary>
/// DTO for unsubscribe requests
/// </summary>
public class UnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}
