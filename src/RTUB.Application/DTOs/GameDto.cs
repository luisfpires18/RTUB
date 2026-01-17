namespace RTUB.Application.DTOs;

/// <summary>
/// Data Transfer Object for Game entity
/// Used to transfer game configuration data between layers
/// </summary>
public class GameDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? PlayRoute { get; set; }
    public bool IsComingSoon { get; set; }
    public bool MembersOnly { get; set; }
    public bool IsActive { get; set; }
}
