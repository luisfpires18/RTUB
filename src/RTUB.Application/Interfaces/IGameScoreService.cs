using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing game scores and leaderboards
/// </summary>
public interface IGameScoreService
{
    /// <summary>
    /// Records a new game score for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="gameId">Game identifier</param>
    /// <param name="score">Points scored</param>
    /// <param name="level">Maximum level reached</param>
    /// <returns>The recorded game score with rank information</returns>
    Task<GameScoreDto> RecordScoreAsync(string userId, string gameId, int score, int level);

    /// <summary>
    /// Gets the leaderboard for a specific game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="limit">Maximum number of entries to return (default: 10)</param>
    /// <returns>Leaderboard entries with rank information</returns>
    Task<IEnumerable<GameScoreDto>> GetLeaderboardAsync(string gameId, int limit = 10);

    /// <summary>
    /// Gets a user's best score for a specific game
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="gameId">Game identifier</param>
    /// <returns>User's best score or null if no scores exist</returns>
    Task<GameScoreDto?> GetUserBestScoreAsync(string userId, string gameId);

    /// <summary>
    /// Gets a user's score history for a specific game
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="gameId">Game identifier</param>
    /// <param name="limit">Maximum number of entries to return (default: 10)</param>
    /// <returns>User's score history ordered by date descending</returns>
    Task<IEnumerable<GameScoreDto>> GetUserHistoryAsync(string userId, string gameId, int limit = 10);
}
