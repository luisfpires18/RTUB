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
}

/// <summary>
/// Contains rank progress information for display
/// </summary>
public class RankProgressInfo
{
    public int CurrentXp { get; set; }
    public int CurrentLevel { get; set; }
    public string CurrentRankName { get; set; } = string.Empty;
    public int XpForCurrentLevel { get; set; }
    public int XpForNextLevel { get; set; }
    public int XpToNextLevel { get; set; }
    public int XpInCurrentLevel { get; set; }
    public int XpNeededForNextLevel { get; set; }
    public double ProgressPercentage { get; set; }
    public bool IsMaxLevel { get; set; }
}
