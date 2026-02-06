using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's progress through Stage Mode in My Tuno
/// Tracks current stage, checkpoints, and unlocked regions
/// </summary>
public class StageProgress : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The current stage the player has reached
    /// </summary>
    public int CurrentStage { get; set; } = 1;

    /// <summary>
    /// The highest stage the player has ever reached
    /// </summary>
    public int HighestStage { get; set; } = 1;

    /// <summary>
    /// The last checkpoint stage the player can return to
    /// Checkpoints: every 10 stages but start after each boss (11, 21, 31...)
    /// then every 20 stages after 100 (101, 121, 141...)
    /// After stage 10000: no checkpoints (Infinite Land)
    /// </summary>
    public int LastCheckpoint { get; set; } = 1;

    /// <summary>
    /// Current region/biome based on stage number
    /// </summary>
    public RegionType CurrentRegion { get; set; } = RegionType.Forest;

    /// <summary>
    /// Number of enemies defeated in the current stage (for multi-enemy stages)
    /// Resets to 0 when advancing to next stage
    /// </summary>
    public int EnemiesDefeatedInCurrentStage { get; set; } = 0;

    /// <summary>
    /// Whether the player has unlocked Endless mode (beat stage 10000 boss)
    /// </summary>
    public bool EndlessModeUnlocked { get; set; } = false;

    /// <summary>
    /// Total stages cleared by this player
    /// </summary>
    public int TotalStagesCleared { get; set; } = 0;

    /// <summary>
    /// Total bosses defeated
    /// </summary>
    public int TotalBossesDefeated { get; set; } = 0;

    /// <summary>
    /// Legacy DB column - kept for compatibility. No longer tracked since miniBoss was removed.
    /// </summary>
    public int TotalMiniBossesDefeated { get; set; } = 0;

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private StageProgress() { }

    /// <summary>
    /// Factory method to create a new stage progress record for a user
    /// </summary>
    public static StageProgress Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new StageProgress
        {
            UserId = userId,
            CurrentStage = 1,
            HighestStage = 1,
            LastCheckpoint = 1,
            CurrentRegion = RegionType.Forest,
            EndlessModeUnlocked = false,
            TotalStagesCleared = 0,
            TotalBossesDefeated = 0
        };
    }

    /// <summary>
    /// Advances to the next stage after clearing the current one
    /// Updates checkpoints and region accordingly
    /// </summary>
    public void AdvanceStage()
    {
        TotalStagesCleared++;
        CurrentStage++;
        EnemiesDefeatedInCurrentStage = 0; // Reset for new stage

        if (CurrentStage > HighestStage)
        {
            HighestStage = CurrentStage;
        }

        // Update checkpoint
        LastCheckpoint = CalculateCheckpoint(CurrentStage);

        // Update region
        CurrentRegion = GetRegionForStage(CurrentStage);
    }

    /// <summary>
    /// Records defeating one enemy in the current stage
    /// </summary>
    public void RecordEnemyDefeat()
    {
        EnemiesDefeatedInCurrentStage++;
    }

    /// <summary>
    /// Records defeating a boss
    /// </summary>
    public void RecordBossDefeat()
    {
        TotalBossesDefeated++;

        // Check if player beat the stage 10000 boss
        if (CurrentStage == 10000)
        {
            EndlessModeUnlocked = true;
        }
    }

    /// <summary>
    /// Returns to the last checkpoint after defeat
    /// </summary>
    public void ReturnToCheckpoint()
    {
        CurrentStage = LastCheckpoint;
        CurrentRegion = GetRegionForStage(CurrentStage);
        EnemiesDefeatedInCurrentStage = 0; // Reset enemy counter
    }

    /// <summary>
    /// Calculates the checkpoint for a given stage
    /// - Before stage 100: checkpoint every 10 stages, starting after boss (1, 11, 21, 31...)
    /// - After stage 100: checkpoint every 20 stages (101, 121, 141...)
    /// - After stage 10000: no new checkpoints (Infinite Land)
    /// </summary>
    public static int CalculateCheckpoint(int stage)
    {
        if (stage <= 1) return 1;

        // Infinite Land - no checkpoints after 10000
        if (stage > 10000)
        {
            return 10000;
        }

        // Before stage 100: checkpoint every 10 stages, starting at 11
        if (stage <= 100)
        {
            var checkpoint = ((stage - 1) / 10) * 10 + 1;
            return checkpoint;
        }

        // After stage 100: checkpoint every 20 stages starting at 101
        var checkpointsAfter100 = (stage - 101) / 20;
        return 101 + (checkpointsAfter100 * 20);
    }

    /// <summary>
    /// Gets the region for a given stage number
    /// Each region spans 100 stages, cycling after stage 1000
    /// Stage 10001+ is Infinite Land
    /// </summary>
    public static RegionType GetRegionForStage(int stage)
    {
        if (stage > 10000) return RegionType.InfiniteLand;

        // Calculate region based on which hundred the stage is in
        // Stages 1-100: Forest (index 0)
        // Stages 101-200: Desert (index 1)
        // etc.
        var regionIndex = (stage - 1) / 100;

        // Cycle through first 10 regions (0-9) for stages above 1000
        regionIndex = regionIndex % 10;

        return (RegionType)regionIndex;
    }

    /// <summary>
    /// Gets the enemy type for a given stage
    /// - Boss stages determined by config (bossEveryNStages)
    /// - All other stages: Normal enemy
    /// </summary>
    public static EnemyType GetEnemyTypeForStage(int stage)
    {
        // Boss every 10 stages (config-driven via bossEveryNStages)
        if (stage % 10 == 0) return EnemyType.Boss;

        return EnemyType.Normal;
    }

    /// <summary>
    /// Checks if the current stage is in Infinite Land
    /// </summary>
    public bool IsInInfiniteLand()
    {
        return CurrentStage > 10000;
    }
}
