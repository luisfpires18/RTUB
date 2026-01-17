using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a game score entry for tracking leaderboards
/// </summary>
public class GameScore : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100, ErrorMessage = "Game ID cannot exceed 100 characters")]
    public string GameId { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Score must be non-negative")]
    public int Score { get; set; }

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Level must be non-negative")]
    public int Level { get; set; }

    [Required]
    public DateTime PlayedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private GameScore() { }

    // Factory method
    public static GameScore Create(string userId, string gameId, int score, int level)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game ID is required", nameof(gameId));
        if (score < 0)
            throw new ArgumentException("Score must be non-negative", nameof(score));
        if (level < 0)
            throw new ArgumentException("Level must be non-negative", nameof(level));

        return new GameScore
        {
            UserId = userId,
            GameId = gameId,
            Score = score,
            Level = level,
            PlayedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
