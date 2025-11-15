namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for the Ranking/Level system
/// Defines XP rewards and level thresholds
/// </summary>
public class RankingConfiguration
{
    public const string SectionName = "Ranking";
    
    public bool Enabled { get; set; } = true;
    public int XpPerRehearsal { get; set; } = 10;
    public Dictionary<string, int> XpPerEventType { get; set; } = new();
    public List<LevelDefinition> Levels { get; set; } = new();
}

/// <summary>
/// Defines a single level/rank in the progression system
/// </summary>
public class LevelDefinition
{
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public int XpThreshold { get; set; }
}
