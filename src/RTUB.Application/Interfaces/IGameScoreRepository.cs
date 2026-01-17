using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for GameScore entity
/// Provides data access operations for game scores and leaderboards
/// </summary>
public interface IGameScoreRepository : IRepository<GameScore>
{
    /// <summary>
    /// Gets top scores for a specific game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="limit">Maximum number of scores to return (default: 10)</param>
    /// <returns>Collection of top scores ordered by score descending</returns>
    Task<IEnumerable<GameScore>> GetTopScoresAsync(string gameId, int limit = 10);

    /// <summary>
    /// Gets user's score history for a specific game
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="gameId">Game identifier</param>
    /// <param name="limit">Maximum number of scores to return (default: 10)</param>
    /// <returns>Collection of user's scores ordered by played date descending</returns>
    Task<IEnumerable<GameScore>> GetUserScoresAsync(string userId, string gameId, int limit = 10);

    /// <summary>
    /// Gets user's best score for a specific game
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="gameId">Game identifier</param>
    /// <returns>User's best score or null if no scores exist</returns>
    Task<GameScore?> GetUserBestScoreAsync(string userId, string gameId);
}
