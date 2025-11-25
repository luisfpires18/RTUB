namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for group participant information
/// </summary>
public class GroupParticipantDto
{
    public string UserId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public string? Avatar { get; set; }
}
