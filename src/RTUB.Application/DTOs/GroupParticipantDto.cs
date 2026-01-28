namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for group participant information
/// </summary>
public class GroupParticipantDto
{
    /// <summary>
    /// Unique identifier for this participant (uses UserId)
    /// </summary>
    public string Id => UserId;

    public string UserId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public string? Avatar { get; set; }
}
