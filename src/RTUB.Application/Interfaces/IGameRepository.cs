using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Game entity
/// Provides specialized data access methods for game configurations
/// </summary>
public interface IGameRepository : IRepository<Game>
{
    /// <summary>
    /// Gets a game by its unique key
    /// </summary>
    /// <param name="key">The game key</param>
    /// <returns>The game if found, otherwise null</returns>
    Task<Game?> GetByKeyAsync(string key);

    /// <summary>
    /// Gets all active games ordered by title
    /// </summary>
    /// <returns>List of active games</returns>
    Task<List<Game>> GetActiveGamesAsync();
}
