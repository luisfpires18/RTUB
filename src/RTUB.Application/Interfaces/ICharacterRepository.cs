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
    /// Gets a character by user ID, forcing a DB reload if the entity is already tracked.
    /// Use on page-load paths to pick up external changes (e.g., owner "Heal All").
    /// </summary>
    Task<Character?> GetByUserIdFreshAsync(string userId);

    /// <summary>
    /// Gets all characters ordered by level descending, then by XP descending
    /// </summary>
    /// <returns>List of characters ordered by level</returns>
    Task<List<Character>> GetAllOrderedByLevelAsync();

    /// <summary>
    /// Gets all arena opponents (all member characters except the specified one)
    /// Ordered by level descending. Excludes the specified character.
    /// </summary>
    /// <param name="excludeCharacterId">Character ID to exclude from results</param>
    /// <returns>List of opponents ordered by level</returns>
    Task<List<Character>> GetArenaOpponentsAsync(int excludeCharacterId);
}
