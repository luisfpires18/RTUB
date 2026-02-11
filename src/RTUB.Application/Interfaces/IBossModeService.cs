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
    /// Checks if the user can enter Boss Mode (has at least 1 FITAB).
    /// </summary>
    Task<bool> CanEnterBossModeAsync(string userId, CancellationToken cancellationToken = default);

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
    /// </summary>
    Task ApplyBossRunRewardsAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0, int? restoreHp = null, Dictionary<InventoryItemType, int>? instrumentParts = null, Dictionary<InventoryItemType, int>? equipment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a boss mode run, restoring character state.
    /// </summary>
    Task<bool> CancelBossRunAsync(int characterId, int restoreHp, int restoreShotBuffBattles = 0, int restoreCigarroShield = 0, int restoreCanhaoBoost = 0, int restorePenaltyBuff = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a random boss sprite path for the current boss stage.
    /// </summary>
    Task<string> GetBossSpriteAsync(int bossStage, CancellationToken cancellationToken = default);

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
