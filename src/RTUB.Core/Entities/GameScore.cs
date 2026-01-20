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

    /// <summary>
    /// Updates the score values if the new score is better
    /// A score is considered better if it has more points, or same points but higher level
    /// </summary>
    /// <returns>True if the score was updated, false if the existing score was better</returns>
    public bool UpdateIfBetter(int points, int maxLevel, TimeSpan timeSurvived)
    {
        if (points < 0)
            throw new ArgumentException("Points cannot be negative", nameof(points));
        if (maxLevel < 0)
            throw new ArgumentException("Max level cannot be negative", nameof(maxLevel));

        if (IsScoreBetter(points, maxLevel))
        {
            Points = points;
            MaxLevel = maxLevel;
            TimeSurvived = timeSurvived;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the new score is better than the current score.
    /// A score is better if it has more points, or same points with higher level.
    /// </summary>
    private bool IsScoreBetter(int newPoints, int newMaxLevel)
    {
        return newPoints > Points || (newPoints == Points && newMaxLevel > MaxLevel);
    }
}
