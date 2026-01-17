using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for GameScore entity
/// </summary>
public interface IGameScoreRepository : IRepository<GameScore>
{
    Task<List<GameScore>> GetTopScoresAsync(string gameKey, int count = 10);
    Task<GameScore?> GetUserBestScoreAsync(string userId, string gameKey);
    
    /// <summary>
    /// Gets the user's score for a specific game (tracked for updates)
    /// </summary>
    Task<GameScore?> GetUserScoreAsync(string userId, string gameKey);
}
