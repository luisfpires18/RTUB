using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for game score operations
/// </summary>
public interface IGameScoreService
{
    Task<GameScore> SubmitScoreAsync(string userId, string gameKey, int points, int maxLevel, TimeSpan timeSurvived);
    Task<List<GameScoreDto>> GetLeaderboardAsync(string gameKey, int count = 10);
    Task<GameScoreDto?> GetUserBestScoreAsync(string userId, string gameKey);
    
    /// <summary>
    /// Filters leaderboard scores by search term (user name or nickname)
    /// </summary>
    /// <param name="scores">Collection of scores to filter</param>
    /// <param name="searchTerm">Search term to filter by</param>
    /// <returns>Filtered list of scores</returns>
    List<GameScoreDto> FilterLeaderboardScores(IEnumerable<GameScoreDto> scores, string searchTerm);
}
