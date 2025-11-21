namespace RTUB.Application.Configuration;

/// <summary>
/// Defines a single level/rank in the progression system
/// </summary>
public class LevelDefinition
{
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public int XpThreshold { get; set; }
}
