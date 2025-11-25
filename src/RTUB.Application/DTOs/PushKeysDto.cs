namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for push subscription keys
/// </summary>
public class PushKeysDto
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}
