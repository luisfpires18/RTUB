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
    Task<BossModeProgress> GetOrCreateBossModeProgressAsync(string userId);

    /// <summary>
    /// Gets boss mode progress for a user.
    /// </summary>
    Task<BossModeProgress?> GetBossModeProgressAsync(string userId);

    /// <summary>
    /// Checks if the user can enter Boss Mode (has at least 1 FITAB).
    /// </summary>
    Task<bool> CanEnterBossModeAsync(string userId);

    /// <summary>
    /// Starts a new Boss Mode run. Deducts 1 FITAB from the user's balance.
    /// </summary>
    Task<BossModeProgress> StartBossModeRunAsync(string userId);

    /// <summary>
    /// Executes a boss battle at the current boss stage.
    /// </summary>
    Task<BossModeBattleResult> ExecuteBossBattleAsync(int characterId);

    /// <summary>
    /// Applies accumulated boss mode run rewards when the run ends.
    /// </summary>
    Task ApplyBossRunRewardsAsync(int characterId, int xp, decimal fidelis, int beers, int shots, int? restoreHp = null, Dictionary<InventoryItemType, int>? instrumentParts = null, Dictionary<InventoryItemType, int>? equipment = null);

    /// <summary>
    /// Cancels a boss mode run, restoring character state.
    /// </summary>
    Task<bool> CancelBossRunAsync(int characterId, int restoreHp, int restoreShotBuffBattles = 0);

    /// <summary>
    /// Gets a random boss sprite path for the current boss stage.
    /// </summary>
    Task<string> GetBossSpriteAsync(int bossStage);

    /// <summary>
    /// Gets the background path for boss mode.
    /// </summary>
    string GetBackgroundPath();

    /// <summary>
    /// Gets the equivalent stage mode difficulty for a boss stage number.
    /// Boss stage 1 = stage 500+ equivalent.
    /// </summary>
    int GetEquivalentStage(int bossStage);
}
