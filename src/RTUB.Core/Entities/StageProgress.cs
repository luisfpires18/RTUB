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
    /// Checkpoints: every 10 stages, starting after each boss (1, 11, 21, 31...)
    /// After stage 1000: no checkpoints (Void)
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
    /// Whether the player has unlocked Endless mode (beat stage 1000 boss)
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
    /// Purchased equipment enhancement bonus level (from Fidelis upgrades).
    /// Total enhancement = floor(HighestStage / 100) + EquipmentBonusLevel.
    /// </summary>
    public int EquipmentBonusLevel { get; set; } = 0;

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
            TotalBossesDefeated = 0,
            EquipmentBonusLevel = 0
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

        // Check if player beat the stage 1000 boss
        if (CurrentStage == 1000)
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
    /// - All stages: checkpoint every 10 stages (1, 11, 21, 31... after each boss)
    /// - After stage 1000: no new checkpoints (Void)
    /// </summary>
    public static int CalculateCheckpoint(int stage)
    {
        if (stage <= 1) return 1;

        // Void - no checkpoints after 1000
        if (stage > 1000)
        {
            return 1000;
        }

        // Checkpoint every 10 stages: 1, 11, 21, 31, ...
        var checkpoint = ((stage - 1) / 10) * 10 + 1;
        return checkpoint;
    }

    /// <summary>
    /// Gets the region for a given stage number
    /// Stages 1-100: Forest, 101-200: Swamp, 201-300: Mountains,
    /// 301-400: Snowy, 401-500: Tropical, 501-600: Caverns,
    /// 601-700: Desert, 701-800: Volcanic, 801-900: Ruins,
    /// 901-1000: Dark, 1001+: Void
    /// </summary>
    public static RegionType GetRegionForStage(int stage)
    {
        if (stage > 1000) return RegionType.Void;

        var regionIndex = (stage - 1) / 100;
        return (RegionType)regionIndex;
    }

    /// <summary>
    /// Gets the enemy type for a given stage
    /// - Boss stages determined by config (bossEveryNStages = 10)
    /// - All other stages: Normal enemy
    /// </summary>
    public static EnemyType GetEnemyTypeForStage(int stage)
    {
        // Boss every 10 stages (config-driven via bossEveryNStages)
        if (stage % 10 == 0) return EnemyType.Boss;

        return EnemyType.Normal;
    }

    /// <summary>
    /// Checks if the current stage is in the Void (endless)
    /// </summary>
    public bool IsInVoid()
    {
        return CurrentStage > 1000;
    }
}
