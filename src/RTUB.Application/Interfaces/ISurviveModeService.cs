using System.Threading;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Survive Mode service.
/// Survive Mode is a survivor.io-inspired game where the player spawns in the map center,
/// enemies swarm from all directions, and the player must dodge/run until the timer expires.
/// Each level corresponds to a biome (Forest → Void). Harder wave patterns, faster enemies,
/// and longer timers as levels increase.
/// </summary>
public interface ISurviveModeService
{
    /// <summary>
    /// Gets or creates survive mode progress for a user.
    /// </summary>
    Task<SurviveModeProgress> GetOrCreateProgressAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets survive mode progress for a user (null if not started).
    /// </summary>
    Task<SurviveModeProgress?> GetProgressAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new survive mode run. Validates character is alive.
    /// </summary>
    Task<SurviveModeProgress> StartRunAsync(int characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the level configuration data needed by the client to render the game.
    /// Includes enemy counts, spawn rates, timer duration, speed multipliers, etc.
    /// </summary>
    SurviveModeLevelConfig GetLevelConfig(int level, int characterLevel);

    /// <summary>
    /// Completes a level — the player survived the timer. Calculates and returns rewards.
    /// Server validates survival time against the run start timestamp.
    /// </summary>
    Task<SurviveModeLevelResult> CompleteLevelAsync(int characterId, int enemiesKilled, double survivalTimeSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the run — the player died before the timer expired.
    /// Calculates partial rewards based on survival time and enemies killed.
    /// </summary>
    Task<SurviveModeLevelResult> EndRunAsync(int characterId, int enemiesKilled, double survivalTimeSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies accumulated survive mode run rewards (XP, Fidelis, drops).
    /// Called when a run ends (either by death or voluntary exit).
    /// </summary>
    Task ApplyRunRewardsAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0,
        Dictionary<InventoryItemType, int>? instrumentParts = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active run without applying rewards.
    /// </summary>
    Task<bool> CancelRunAsync(int characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the background sprite path for a given level.
    /// </summary>
    string GetBackgroundPath(int level);

    /// <summary>
    /// Gets a random set of enemy sprite paths for a given level.
    /// </summary>
    Task<List<string>> GetEnemySpritesAsync(int level, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets boss sprite paths for a given level's biome.
    /// Returns up to <paramref name="count"/> randomly selected boss sprites.
    /// </summary>
    Task<List<string>> GetBossSpritesAsync(int level, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current level for a user (level selection).
    /// Validates that the target level is within the user's reached range.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="targetLevel">The level to start from</param>
    /// <returns>Updated survive mode progress</returns>
    Task<SurviveModeProgress> SetStartLevelAsync(string userId, int targetLevel, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration data for a survive mode level, sent to the client game engine.
/// </summary>
public class SurviveModeLevelConfig
{
    /// <summary>
    /// The level number.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Biome name (e.g., "Forest", "Swamp").
    /// </summary>
    public string BiomeName { get; set; } = string.Empty;

    /// <summary>
    /// The region type enum value.
    /// </summary>
    public RegionType Region { get; set; }

    /// <summary>
    /// How long (in seconds) the player must survive to beat this level.
    /// </summary>
    public double TimerDurationSeconds { get; set; }

    /// <summary>
    /// Base number of enemies alive at once at the start.
    /// </summary>
    public int BaseEnemyCount { get; set; }

    /// <summary>
    /// Maximum number of enemies alive at once (grows during the level).
    /// </summary>
    public int MaxEnemyCount { get; set; }

    /// <summary>
    /// Interval in seconds between enemy spawn waves.
    /// </summary>
    public double SpawnIntervalSeconds { get; set; }

    /// <summary>
    /// Base enemy movement speed (pixels per second).
    /// </summary>
    public double EnemySpeed { get; set; }

    /// <summary>
    /// Maximum enemy speed (enemies get faster over time within the level).
    /// </summary>
    public double MaxEnemySpeed { get; set; }

    /// <summary>
    /// Player movement speed (pixels per second).
    /// </summary>
    public double PlayerSpeed { get; set; }

    /// <summary>
    /// Size of enemy sprites (scale factor, 1.0 = normal).
    /// Smaller = harder to dodge due to density.
    /// </summary>
    public double EnemyScale { get; set; }

    /// <summary>
    /// Difficulty multiplier (affects reward calculations).
    /// </summary>
    public double DifficultyMultiplier { get; set; }

    /// <summary>
    /// Reward multiplier for this biome.
    /// </summary>
    public double RewardMultiplier { get; set; }

    /// <summary>
    /// Number of distinct enemy sprite variants available for this biome.
    /// </summary>
    public int EnemySpriteVariants { get; set; }

    /// <summary>
    /// Whether elite enemies (faster, larger) spawn in this level.
    /// </summary>
    public bool HasEliteEnemies { get; set; }

    /// <summary>
    /// Chance for an elite enemy to spawn (0.0–1.0).
    /// </summary>
    public double EliteSpawnChance { get; set; }

    /// <summary>
    /// Map width in pixels.
    /// </summary>
    public int MapWidth { get; set; }

    /// <summary>
    /// Map height in pixels.
    /// </summary>
    public int MapHeight { get; set; }

    /// <summary>
    /// The viewport width (visible game area).
    /// </summary>
    public int ViewportWidth { get; set; }

    /// <summary>
    /// The viewport height (visible game area).
    /// </summary>
    public int ViewportHeight { get; set; }

    /// <summary>
    /// Boss sprite paths for this level's biome. Empty for levels with no bosses (e.g., Void).
    /// </summary>
    public List<string> BossSprites { get; set; } = new();

    /// <summary>
    /// Whether this is the final level (Void). No bosses, timer expiry = win.
    /// </summary>
    public bool IsFinalLevel { get; set; }

    /// <summary>
    /// Per-minute spawn ramp rate for this biome (e.g., 0.20 = +20% more spawns per minute).
    /// </summary>
    public double SpawnRampPerMinute { get; set; }

    /// <summary>
    /// Per-minute enemy speed ramp rate for this biome (e.g., 0.10 = +10% faster per minute).
    /// </summary>
    public double SpeedRampPerMinute { get; set; }
}
