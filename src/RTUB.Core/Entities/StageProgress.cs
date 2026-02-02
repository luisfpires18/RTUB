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
    /// Checkpoints: every 10 stages until stage 100, then every 20 stages
    /// After stage 10000: no checkpoints (Infinite Land)
    /// </summary>
    public int LastCheckpoint { get; set; } = 1;

    /// <summary>
    /// Current region/biome based on stage number
    /// </summary>
    public RegionType CurrentRegion { get; set; } = RegionType.Forest;

    /// <summary>
    /// Whether the player has unlocked Endless mode (beat stage 10000 boss)
    /// </summary>
    public bool EndlessModeUnlocked { get; set; } = false;

    /// <summary>
    /// Total stages cleared by this player
    /// </summary>
    public int TotalStagesCleared { get; set; } = 0;

    /// <summary>
    /// Total mini-bosses defeated
    /// </summary>
    public int TotalMiniBossesDefeated { get; set; } = 0;

    /// <summary>
    /// Total bosses defeated
    /// </summary>
    public int TotalBossesDefeated { get; set; } = 0;

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
            TotalMiniBossesDefeated = 0,
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
    /// Records defeating a mini-boss
    /// </summary>
    public void RecordMiniBossDefeat()
    {
        TotalMiniBossesDefeated++;
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
    }

    /// <summary>
    /// Calculates the checkpoint for a given stage
    /// - Before stage 100: checkpoint every 10 stages (1, 10, 20, 30...)
    /// - After stage 100: checkpoint every 20 stages (100, 120, 140...)
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

        // Before stage 100: checkpoint every 10 stages
        if (stage <= 100)
        {
            var checkpoint = (stage / 10) * 10;
            // Stages 1-9 should use checkpoint 1 (starting point)
            return checkpoint == 0 ? 1 : checkpoint;
        }

        // After stage 100: checkpoint every 20 stages
        // Last checkpoint at 100 is 100, then 120, 140, etc.
        var checkpointsAfter100 = (stage - 100) / 20;
        return 100 + (checkpointsAfter100 * 20);
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
    /// - Every 100 stages: Boss
    /// - Every 10 stages (not 100): Mini-boss
    /// - Other stages: Normal enemy
    /// Note: After stage 10000 (Infinite Land), no mini-bosses appear
    /// </summary>
    public static EnemyType GetEnemyTypeForStage(int stage)
    {
        if (stage % 100 == 0) return EnemyType.Boss;

        // No mini-bosses in Infinite Land (after stage 10000)
        if (stage > 10000) return EnemyType.Normal;

        if (stage % 10 == 0) return EnemyType.MiniBoss;

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
