using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Character entity
/// Provides specialized data access methods for characters
/// </summary>
public interface ICharacterRepository : IRepository<Character>
{
    /// <summary>
    /// Gets a character by user ID
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The character if found, otherwise null</returns>
    Task<Character?> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets all characters ordered by level descending, then by XP descending
    /// </summary>
    /// <returns>List of characters ordered by level</returns>
    Task<List<Character>> GetAllOrderedByLevelAsync();

    /// <summary>
    /// Gets all characters for member users (excluding a specific character)
    /// </summary>
    /// <param name="excludeCharacterId">Character ID to exclude from results</param>
    /// <returns>List of member characters ordered by level</returns>
    Task<List<Character>> GetMemberCharactersAsync(int excludeCharacterId);

    /// <summary>
    /// Gets opponents for a character, prioritizing by level difference
    /// Priority: 1) Higher level (closest first), 2) Same level, 3) Lower level (closest first)
    /// </summary>
    /// <param name="excludeCharacterId">Character ID to exclude from results (player character)</param>
    /// <param name="count">Number of opponents to return (default: 8)</param>
    /// <returns>List of prioritized opponents</returns>
    Task<List<Character>> GetRandomOpponentsAsync(int excludeCharacterId, int count = 8);

    /// <summary>
    /// Gets top leaderboard entries ranked by wins, then level
    /// </summary>
    /// <param name="count">Number of entries to return</param>
    /// <returns>List of leaderboard entries</returns>
    Task<List<MyTunoLeaderboardEntry>> GetTopLeaderboardAsync(int count);
}
