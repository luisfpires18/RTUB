using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's progress through Survive Mode in My Tuno.
/// Survive Mode is a survivor.io-inspired game where the player spawns in the center
/// of a map, enemies swarm from all edges, and the player must survive until the timer
/// runs out. Each level corresponds to a Stage Mode biome (Forest → Void).
/// Harder than Stage Mode — enemy waves increase, timer is longer, enemies are faster.
/// </summary>
public class SurviveModeProgress : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The current survive level the player has reached (1 = Forest, 2 = Swamp, etc.)
    /// </summary>
    public int CurrentLevel { get; set; } = 1;

    /// <summary>
    /// The highest survive level the player has ever reached.
    /// </summary>
    public int HighestLevel { get; set; } = 1;

    /// <summary>
    /// Current region/biome based on level number.
    /// Level 1 = Forest, Level 2 = Swamp, ..., Level 11 = Void.
    /// </summary>
    public RegionType CurrentRegion { get; set; } = RegionType.Forest;

    /// <summary>
    /// The longest time (seconds) the player has survived in a single run.
    /// </summary>
    public double LongestSurvivalTime { get; set; } = 0;

    /// <summary>
    /// Total enemies killed across all survive mode runs.
    /// </summary>
    public int TotalEnemiesKilled { get; set; } = 0;

    /// <summary>
    /// Total levels completed across all runs.
    /// </summary>
    public int TotalLevelsCompleted { get; set; } = 0;

    /// <summary>
    /// Total number of runs attempted.
    /// </summary>
    public int TotalRunsAttempted { get; set; } = 0;

    /// <summary>
    /// Whether the player is currently in an active run.
    /// </summary>
    public bool IsRunActive { get; set; } = false;

    /// <summary>
    /// The level the current active run started at.
    /// </summary>
    public int RunStartLevel { get; set; } = 1;

    /// <summary>
    /// Timestamp when the current run started (for server-side validation).
    /// </summary>
    public DateTime? RunStartedAt { get; set; }

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private SurviveModeProgress() { }

    /// <summary>
    /// Factory method to create a new survive mode progress record for a user.
    /// </summary>
    public static SurviveModeProgress Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new SurviveModeProgress
        {
            UserId = userId,
            CurrentLevel = 1,
            HighestLevel = 1,
            CurrentRegion = RegionType.Forest,
            LongestSurvivalTime = 0,
            TotalEnemiesKilled = 0,
            TotalLevelsCompleted = 0,
            TotalRunsAttempted = 0,
            IsRunActive = false,
            RunStartLevel = 1
        };
    }

    /// <summary>
    /// Starts a new survive mode run from the player's current level.
    /// </summary>
    public void StartRun()
    {
        IsRunActive = true;
        RunStartLevel = CurrentLevel;
        RunStartedAt = DateTime.UtcNow;
        TotalRunsAttempted++;
    }

    /// <summary>
    /// Completes a level — the player survived the timer.
    /// Advances to the next level.
    /// </summary>
    public void CompleteLevel(double survivalTimeSeconds, int enemiesKilled)
    {
        TotalLevelsCompleted++;
        TotalEnemiesKilled += enemiesKilled;

        if (survivalTimeSeconds > LongestSurvivalTime)
            LongestSurvivalTime = survivalTimeSeconds;

        CurrentLevel++;
        if (CurrentLevel > HighestLevel)
            HighestLevel = CurrentLevel;

        CurrentRegion = GetRegionForLevel(CurrentLevel);
    }

    /// <summary>
    /// Ends the run — the player was caught/killed before the timer expired.
    /// </summary>
    public void EndRun(double survivalTimeSeconds, int enemiesKilled)
    {
        TotalEnemiesKilled += enemiesKilled;

        if (survivalTimeSeconds > LongestSurvivalTime)
            LongestSurvivalTime = survivalTimeSeconds;

        IsRunActive = false;
        RunStartedAt = null;

        // On death, reset to current level (no regression — just restart the same level)
    }

    /// <summary>
    /// Cancels an active run without applying rewards.
    /// </summary>
    public void CancelRun()
    {
        IsRunActive = false;
        RunStartedAt = null;
        CurrentLevel = RunStartLevel;
        CurrentRegion = GetRegionForLevel(CurrentLevel);
    }

    /// <summary>
    /// Gets the region/biome for a given survive level.
    /// Level 1 = Forest, 2 = Swamp, ... 10 = Dark, 11+ = Void.
    /// </summary>
    public static RegionType GetRegionForLevel(int level)
    {
        return level switch
        {
            1 => RegionType.Forest,
            2 => RegionType.Swamp,
            3 => RegionType.Mountains,
            4 => RegionType.Snowy,
            5 => RegionType.Tropical,
            6 => RegionType.Caverns,
            7 => RegionType.Desert,
            8 => RegionType.Volcanic,
            9 => RegionType.Ruins,
            10 => RegionType.Dark,
            _ => RegionType.Void,
        };
    }

    /// <summary>
    /// Gets the biome name for a given level.
    /// </summary>
    public static string GetBiomeName(int level)
    {
        return GetRegionForLevel(level) switch
        {
            RegionType.Forest => "Forest",
            RegionType.Swamp => "Swamp",
            RegionType.Mountains => "Mountains",
            RegionType.Snowy => "Snowy",
            RegionType.Tropical => "Tropical",
            RegionType.Caverns => "Caverns",
            RegionType.Desert => "Desert",
            RegionType.Volcanic => "Volcanic",
            RegionType.Ruins => "Ruins",
            RegionType.Dark => "Dark",
            RegionType.Void => "Void",
            _ => "Unknown"
        };
    }
}
