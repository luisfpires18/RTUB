namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for the Ranking/Level system
/// Defines XP rewards and level thresholds
/// </summary>
public class RankingConfiguration
{
    public const string SectionName = "Ranking";
    public int XpPerRehearsal { get; set; } = 10;
    public Dictionary<string, int> XpPerEventType { get; set; } = new();
    public List<LevelDefinition> Levels { get; set; } = new();
}
