namespace RTUB.Application.Configuration;

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
