using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Battle entity
/// Provides data access operations for battles
/// </summary>
public interface IBattleRepository : IRepository<Battle>
{
    /// <summary>
    /// Gets all battles for a specific character (as attacker)
    /// </summary>
    Task<List<Battle>> GetByAttackerCharacterIdAsync(int characterId);

    /// <summary>
    /// Gets all battles for a specific character (as defender)
    /// </summary>
    Task<List<Battle>> GetByDefenderCharacterIdAsync(int characterId);

    /// <summary>
    /// Gets all battles for a specific character (as attacker or defender)
    /// </summary>
    Task<List<Battle>> GetByCharacterIdAsync(int characterId);

    /// <summary>
    /// Gets battles for a character filtered by outcome
    /// </summary>
    Task<List<Battle>> GetByCharacterIdAndOutcomeAsync(int characterId, BattleOutcome outcome);

    /// <summary>
    /// Gets recent battles for a character (ordered by CreatedAt descending)
    /// </summary>
    Task<List<Battle>> GetRecentBattlesByCharacterIdAsync(int characterId, int count);

    /// <summary>
    /// Gets top leaderboard entries ranked by wins, then level
    /// </summary>
    Task<List<RTUB.Application.DTOs.MyTunoLeaderboardEntry>> GetTopLeaderboardAsync(int count);

    /// <summary>
    /// Gets battles between two specific characters
    /// Used for cooldown checking (prevent fighting same opponent too frequently)
    /// </summary>
    Task<List<Battle>> GetBattlesBetweenCharactersAsync(int attackerId, int defenderId, TimeSpan? withinTimeSpan = null);
}
