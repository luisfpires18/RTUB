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
