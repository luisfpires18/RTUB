namespace RTUB.Application.DTOs;

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
