using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's score in a game
/// </summary>
public class GameScore : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string GameKey { get; set; } = string.Empty;

    public int Points { get; set; }

    public int MaxLevel { get; set; }

    public TimeSpan TimeSurvived { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;

    private GameScore() { }

    public static GameScore Create(string userId, string gameKey, int points, int maxLevel, TimeSpan timeSurvived)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(gameKey))
            throw new ArgumentException("Game key is required", nameof(gameKey));
        if (points < 0)
            throw new ArgumentException("Points cannot be negative", nameof(points));
        if (maxLevel < 0)
            throw new ArgumentException("Max level cannot be negative", nameof(maxLevel));

        return new GameScore
        {
            UserId = userId,
            GameKey = gameKey,
            Points = points,
            MaxLevel = maxLevel,
            TimeSurvived = timeSurvived
        };
    }
}
