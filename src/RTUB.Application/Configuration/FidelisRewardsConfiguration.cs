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

    /// <summary>
    /// Fidelis rewards configuration for PassaroMaluco game
    /// </summary>
    public GameRewardSettings PassaroMaluco { get; set; } = new();

    /// <summary>
    /// Fidelis rewards configuration for TomatoThrower game
    /// </summary>
    public GameRewardSettings TomatoThrower { get; set; } = new();
}

