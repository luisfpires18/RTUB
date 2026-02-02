using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Stage Battle repository
/// Handles data access for stage battle records
/// </summary>
public interface IStageBattleRepository : IRepository<StageBattle>
{
    /// <summary>
    /// Gets all stage battles for a character
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <returns>List of stage battles</returns>
    Task<List<StageBattle>> GetByCharacterIdAsync(int characterId);

    /// <summary>
    /// Gets recent stage battles for a character
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="count">Number of recent battles to retrieve</param>
    /// <returns>List of recent stage battles</returns>
    Task<List<StageBattle>> GetRecentByCharacterIdAsync(int characterId, int count);
}
