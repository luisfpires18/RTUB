namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for difficulty scaling that increases indefinitely
/// </summary>
public class DifficultyScalingConfig
{
    /// <summary>
    /// Base spawn rate in seconds between enemy spawns
    /// </summary>
    public double BaseSpawnRate { get; set; } = 3.5;

    /// <summary>
    /// Spawn rate decrease per level (faster spawns)
    /// </summary>
    public double SpawnRateDecreasePerLevel { get; set; } = 0.15;

    /// <summary>
    /// Minimum spawn rate (cannot go lower)
    /// </summary>
    public double MinSpawnRate { get; set; } = 1.0;

    /// <summary>
    /// Enemy speed increase percentage per level
    /// </summary>
    public double EnemySpeedIncreasePerLevel { get; set; } = 5;

    /// <summary>
    /// Platform gap increase per level (makes jumping harder)
    /// </summary>
    public double PlatformGapIncreasePerLevel { get; set; } = 5;
}
