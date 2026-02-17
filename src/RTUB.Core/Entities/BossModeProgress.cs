using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's progress through Boss Mode in My Tuno.
/// Boss Mode is an endless mode where every stage is a boss fight.
/// Requires FITAB currency to enter (1 per run).
/// Bosses start at stage mode 500+ difficulty scale.
/// Boss HP and stage persist within the same day (daily reset at 00:00 UTC).
/// </summary>
public class BossModeProgress : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The current boss stage the player has reached in their current run.
    /// Set to DailyBossStage at the start of each new run, 0 when no run is active.
    /// </summary>
    public int CurrentBossStage { get; set; } = 0;

    /// <summary>
    /// The highest boss stage the player has ever reached (all-time best).
    /// </summary>
    public int HighestBossStage { get; set; } = 0;

    /// <summary>
    /// Total boss stages cleared across all runs.
    /// </summary>
    public int TotalBossStagesCleared { get; set; } = 0;

    /// <summary>
    /// Total FITAB spent entering Boss Mode.
    /// </summary>
    public int TotalFitabSpent { get; set; } = 0;

    /// <summary>
    /// Total runs attempted (number of times the player entered Boss Mode).
    /// </summary>
    public int TotalRunsAttempted { get; set; } = 0;

    /// <summary>
    /// The boss stage the player is fighting today. Persists across runs within the day.
    /// Resets to 1 at daily reset (00:00 UTC).
    /// </summary>
    public int DailyBossStage { get; set; } = 1;

    /// <summary>
    /// Remaining HP of the current daily boss. Null means the boss has full HP.
    /// Persists across runs — if you take 20% of a boss's HP in one run,
    /// the next run starts with the boss at 80%.
    /// Resets when the boss is defeated or at daily reset.
    /// </summary>
    public long? DailyBossRemainingHP { get; set; }

    /// <summary>
    /// The max HP the current daily boss was created with.
    /// Used to validate boss consistency across runs.
    /// </summary>
    public long DailyBossMaxHP { get; set; } = 0;

    /// <summary>
    /// The date of the last daily reset. Used to detect when a new day has started
    /// and trigger the daily reset (DailyBossStage → 1, clear remaining HP).
    /// </summary>
    public DateTime LastDailyResetDate { get; set; } = DateTime.MinValue;

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private BossModeProgress() { }

    /// <summary>
    /// Factory method to create a new boss mode progress record for a user.
    /// </summary>
    public static BossModeProgress Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new BossModeProgress
        {
            UserId = userId,
            CurrentBossStage = 0,
            HighestBossStage = 0,
            TotalBossStagesCleared = 0,
            TotalFitabSpent = 0,
            TotalRunsAttempted = 0,
            DailyBossStage = 1,
            DailyBossRemainingHP = null,
            DailyBossMaxHP = 0,
            LastDailyResetDate = DateTime.UtcNow.Date
        };
    }

    /// <summary>
    /// Checks if a daily reset is needed (new UTC day) and performs it.
    /// Resets DailyBossStage to 1 and clears boss remaining HP.
    /// Returns true if a reset was performed.
    /// </summary>
    public bool CheckAndApplyDailyReset()
    {
        var today = DateTime.UtcNow.Date;
        if (LastDailyResetDate.Date >= today)
            return false;

        DailyBossStage = 1;
        DailyBossRemainingHP = null;
        DailyBossMaxHP = 0;
        LastDailyResetDate = today;
        return true;
    }

    /// <summary>
    /// Starts a new Boss Mode run. Increments run counter and sets current stage
    /// to the daily boss stage (persists across runs within the day).
    /// </summary>
    public void StartRun()
    {
        CheckAndApplyDailyReset();
        CurrentBossStage = DailyBossStage;
        TotalRunsAttempted++;
        TotalFitabSpent++;
    }

    /// <summary>
    /// Advances to the next boss stage after defeating the current boss.
    /// Also advances the daily boss stage and clears remaining HP.
    /// </summary>
    public void AdvanceBossStage()
    {
        TotalBossStagesCleared++;
        CurrentBossStage++;
        DailyBossStage = CurrentBossStage;
        DailyBossRemainingHP = null;
        DailyBossMaxHP = 0;

        if (CurrentBossStage > HighestBossStage)
        {
            HighestBossStage = CurrentBossStage;
        }
    }

    /// <summary>
    /// Saves the remaining HP of the current boss when the player is defeated.
    /// The boss HP persists so the next run continues where this one left off.
    /// </summary>
    public void SaveBossHP(long remainingHP, long maxHP)
    {
        DailyBossRemainingHP = remainingHP;
        DailyBossMaxHP = maxHP;
    }

    /// <summary>
    /// Ends the current run (on defeat). Resets current stage to 0 but
    /// preserves DailyBossStage and DailyBossRemainingHP for the next run.
    /// </summary>
    public void EndRun()
    {
        // Record the highest before resetting
        if (CurrentBossStage > HighestBossStage)
        {
            HighestBossStage = CurrentBossStage;
        }
        CurrentBossStage = 0;
    }
}
