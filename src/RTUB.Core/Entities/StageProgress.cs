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
    /// After stage 1100: no checkpoints (Void)
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
    /// Whether the player has unlocked Endless mode (beat stage 1100 boss)
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
    /// Advances to the next floor after clearing the current one.
    /// After floor 20000 (last Light boss), jumps to 20001 (Arena).
    /// </summary>
    public void AdvanceStage()
    {
        TotalStagesCleared++;

        // Jump from floor 20000 (end of biomes) to 20001 (Arena start)
        if (CurrentStage == 20000)
        {
            CurrentStage = 20001;
        }
        else
        {
            CurrentStage++;
        }

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
    /// Records defeating a boss.
    /// Unlocks Arena (endless mode) when the player beats floor 20000 boss.
    /// </summary>
    public void RecordBossDefeat()
    {
        TotalBossesDefeated++;

        // Check if player beat floor 20000 boss (last Light boss, unlocks Arena)
        if (CurrentStage == 20000)
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
    /// Calculates the checkpoint for a given floor.
    /// Checkpoint every 10 floors (after each miniboss): 1, 11, 21, ...
    /// After floor 20000: no new checkpoints (Arena).
    /// </summary>
    public static int CalculateCheckpoint(int stage)
    {
        if (stage <= 1) return 1;

        // Arena — no checkpoints after 20000
        if (stage > 20000)
        {
            return 20001;
        }

        // Checkpoint every 10 floors: 1, 11, 21, 31, ...
        var checkpoint = ((stage - 1) / 10) * 10 + 1;
        return checkpoint;
    }

    /// <summary>
    /// Gets the region for a given floor number.
    /// 20 biomes × 1000 floors (1-20000), Arena is 20001+ (endless).
    /// Floors 1-1000: Forest, 1001-2000: Swamp, ..., 19001-20000: Light, 20001+: Arena
    /// </summary>
    public static RegionType GetRegionForStage(int stage)
    {
        if (stage > 20000) return RegionType.Arena;

        var regionIndex = (stage - 1) / 1000;
        if (regionIndex > 19) regionIndex = 19;
        return (RegionType)regionIndex;
    }

    /// <summary>
    /// Gets the enemy type for a given stage.
    /// Boss every 100 stages, MiniBoss every 10 (excluding boss stages).
    /// </summary>
    public static EnemyType GetEnemyTypeForStage(int stage)
    {
        if (stage % 100 == 0) return EnemyType.Boss;
        if (stage % 10 == 0) return EnemyType.MiniBoss;
        return EnemyType.Normal;
    }

    /// <summary>
    /// Checks if the current floor is in the Arena (endless, 20001+).
    /// </summary>
    public bool IsInArena()
    {
        return CurrentStage > 20000;
    }

    /// <summary>
    /// Legacy alias for IsInArena — kept for compatibility.
    /// </summary>
    public bool IsInVoid() => IsInArena();
}
