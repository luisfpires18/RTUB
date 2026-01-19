namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for Fidelis rewards in games
/// Bound from appsettings.json "Games:FidelisRewards" section
/// </summary>
public class FidelisRewardsConfiguration
{
    public const string SectionName = "Games:FidelisRewards";

    /// <summary>
    /// Fidelis rewards configuration for BmrBebeMaisRui game
    /// </summary>
    public GameRewardSettings BmrBebeMaisRui { get; set; } = new();

    /// <summary>
    /// Fidelis rewards configuration for AvoidQuestions game
    /// </summary>
    public GameRewardSettings AvoidQuestions { get; set; } = new();
}

/// <summary>
/// Reward settings for an individual game
/// </summary>
public class GameRewardSettings
{
    /// <summary>
    /// Base Fidelis reward just for playing the game
    /// </summary>
    public int PlayReward { get; set; } = 1;

    /// <summary>
    /// Point-based reward thresholds
    /// </summary>
    public List<PointsThreshold> PointsThresholds { get; set; } = new();

    /// <summary>
    /// Calculate Fidelis reward based on points scored
    /// </summary>
    public int CalculateReward(int points)
    {
        // Start with play reward
        int reward = PlayReward;

        // Find the highest threshold the player has achieved
        var thresholds = PointsThresholds
            .Where(t => points >= t.MinPoints)
            .OrderByDescending(t => t.MinPoints)
            .ToList();

        if (thresholds.Any())
        {
            reward += thresholds.First().Reward;
        }

        return reward;
    }
}

/// <summary>
/// Represents a points threshold with associated reward
/// </summary>
public class PointsThreshold
{
    /// <summary>
    /// Minimum points required to reach this threshold
    /// </summary>
    public int MinPoints { get; set; }

    /// <summary>
    /// Additional Fidelis reward for reaching this threshold
    /// </summary>
    public int Reward { get; set; }
}
