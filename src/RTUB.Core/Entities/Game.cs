using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a game configuration that can be edited by admins
/// </summary>
public class Game : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? ImageUrl { get; set; }

    [MaxLength(100)]
    public string? PlayRoute { get; set; }

    public bool IsComingSoon { get; set; }

    /// <summary>
    /// If true, hide this game from users with LEITAO category
    /// </summary>
    public bool MembersOnly { get; set; }

    public bool IsActive { get; set; } = true;

    private Game() { }

    public static Game Create(string key, string title, string? description = null, string? imageUrl = null, string? playRoute = null, bool isComingSoon = false, bool membersOnly = false)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Game key is required", nameof(key));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Game title is required", nameof(title));

        return new Game
        {
            Key = key,
            Title = title,
            Description = description,
            ImageUrl = imageUrl,
            PlayRoute = playRoute,
            IsComingSoon = isComingSoon,
            MembersOnly = membersOnly,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string? description, bool isComingSoon, bool membersOnly)
    {
        Title = title;
        Description = description;
        IsComingSoon = isComingSoon;
        MembersOnly = membersOnly;
        UpdatedAt = DateTime.UtcNow;
    }
}
