namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for Stage Mode feature
/// Maps from scaling.config.json myTuno.stages section
/// </summary>
public class StageConfiguration
{
    /// <summary>
    /// Default beer drop chance for all stages (can be overridden per stage)
    /// </summary>
    public double BeerDropChance { get; set; } = 0.2;
    
    /// <summary>
    /// Default shot drop chance for all stages (can be overridden per stage)
    /// </summary>
    public double ShotDropChance { get; set; } = 0.1;
    
    /// <summary>
    /// List of all configured stages
    /// </summary>
    public List<StageDefinition> StageList { get; set; } = new();
    
    /// <summary>
    /// Stats bonuses provided by each instrument type
    /// </summary>
    public Dictionary<string, InstrumentStatBonus> InstrumentStats { get; set; } = new();
    
    /// <summary>
    /// Effects of consumable items
    /// </summary>
    public Dictionary<string, ItemEffect> ItemEffects { get; set; } = new();
}

/// <summary>
/// Definition of a single stage
/// </summary>
public class StageDefinition
{
    public int StageNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredLevel { get; set; }
    public string EnemyConfigKey { get; set; } = string.Empty;
    public string RewardInstrument { get; set; } = string.Empty;
    public decimal FidelisReward { get; set; }
    public EnemyStats EnemyStats { get; set; } = new();
}

/// <summary>
/// Enemy stats for a stage
/// </summary>
public class EnemyStats
{
    public int Hp { get; set; }
    public int Power { get; set; }
    public int Speed { get; set; }
    public double CriticalChance { get; set; }
}

/// <summary>
/// Stat bonuses provided by an instrument
/// </summary>
public class InstrumentStatBonus
{
    public int HpBonus { get; set; }
    public int PowerBonus { get; set; }
    public int SpeedBonus { get; set; }
    public double CriticalChanceBonus { get; set; }
}

/// <summary>
/// Effect of a consumable item
/// </summary>
public class ItemEffect
{
    /// <summary>
    /// HP restore percentage (0.0 to 1.0, where 1.0 = 100%)
    /// </summary>
    public double HpRestore { get; set; }
    
    public string Description { get; set; } = string.Empty;
}
