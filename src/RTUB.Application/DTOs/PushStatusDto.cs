namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for push feature status response
/// </summary>
public class PushStatusDto
{
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public string? VapidPublicKey { get; set; }
}
