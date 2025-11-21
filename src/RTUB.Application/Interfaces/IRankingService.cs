using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for ranking/level system operations
/// Calculates XP and determines user levels based on attendance
/// </summary>
public interface IRankingService
{
    /// <summary>
    /// Calculates total XP for a user based on rehearsal and event attendance
    /// </summary>
    Task<int> CalculateTotalXpAsync(string userId);
    
    /// <summary>
    /// Determines the level based on XP amount
    /// </summary>
    int GetLevelFromXp(int xp);
    
    /// <summary>
    /// Gets the rank name for a given level
    /// </summary>
    string GetRankName(int level);
    
    /// <summary>
    /// Gets XP required for the next level
    /// </summary>
    int GetXpForNextLevel(int currentLevel);
    
    /// <summary>
    /// Gets XP required for current level
    /// </summary>
    int GetXpForCurrentLevel(int currentLevel);
    
    /// <summary>
    /// Updates user's XP and level in the database
    /// </summary>
    Task UpdateUserRankingAsync(string userId);
    
    /// <summary>
    /// Gets the rank progress information for a user
    /// </summary>
    Task<RankProgressInfo> GetRankProgressAsync(string userId);
    
    /// <summary>
    /// Gets the rank progress information for multiple users in a single batch operation
    /// Optimized to avoid N+1 queries
    /// </summary>
    Task<Dictionary<string, RankProgressInfo>> GetRankProgressBatchAsync(IEnumerable<string> userIds);
}
