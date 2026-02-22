using System.Threading;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Boss Mode service.
/// Handles boss-only endless progression, FITAB entry fee, and boss battles.
/// </summary>
public interface IBossModeService
{
    /// <summary>
    /// Gets or creates boss mode progress for a user.
    /// </summary>
    Task<BossModeProgress> GetOrCreateBossModeProgressAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets boss mode progress for a user.
    /// </summary>
    Task<BossModeProgress?> GetBossModeProgressAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new Boss Mode run. Deducts 1 FITAB from the user's balance.
    /// </summary>
    Task<BossModeProgress> StartBossModeRunAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a boss battle at the current boss stage.
    /// </summary>
    Task<BossModeBattleResult> ExecuteBossBattleAsync(int characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies accumulated boss mode run rewards when the run ends.
    /// When expireShotBuff/expirePenaltyBuff are true, the corresponding buff
    /// is expired once (1 charge consumed per run, not per boss).
    /// </summary>
    Task ApplyBossRunRewardsAsync(int characterId, decimal fidelis, int leitao, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0, long? restoreHp = null, Dictionary<InventoryItemType, int>? instrumentParts = null, bool expireShotBuff = false, bool expirePenaltyBuff = false, int startBossFloor = 0, int endBossFloor = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a boss mode run, restoring character state.
    /// </summary>
    Task<bool> CancelBossRunAsync(int characterId, long restoreHp, int restoreShotBuffBattles = 0, int restoreCigarroShield = 0, DateTime? restoreCanhaoExpiresAt = null, DateTime? restorePenaltyExpiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a random boss sprite path for the current boss stage.
    /// </summary>
    Task<string> GetBossSpriteAsync(int bossStage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the background path for boss mode.
    /// </summary>
    string GetBackgroundPath();

    /// <summary>
    /// Confirms a boss victory after the interactive combat session agrees with the
    /// pre-computed win. Advances the boss stage — deferred from ExecuteBossBattleAsync
    /// to prevent stage advancement if the player disconnects mid-animation.
    /// </summary>
    Task ConfirmBossVictoryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a defeat when the interactive session overrides a pre-computed win.
    /// Saves the boss's remaining HP and ends the run.
    /// </summary>
    Task RecordInteractiveDefeatAsync(string userId, long bossRemainingHP, long bossMaxHP, CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects server-side boss progress when the interactive combat session
    /// wins a battle that the deterministic engine predicted as a loss.
    /// Undoes the EndRun that was saved during ExecuteBossBattleAsync and advances the boss stage.
    /// </summary>
    Task CorrectInteractiveWinAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites the persisted boss remaining HP for the current daily boss.
    /// Called after the interactive combat session finishes to correct the pre-computed
    /// HP that was saved during ExecuteBossBattleAsync.
    /// Works regardless of whether the run is still active (CurrentBossStage may be 0).
    /// </summary>
    Task SaveBossRemainingHPAsync(string userId, long bossRemainingHP, long bossMaxHP, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the equivalent stage mode difficulty for a boss stage number.
    /// Boss stage 1 = stage 500+ equivalent.
    /// </summary>
    int GetEquivalentStage(int bossStage);
}
