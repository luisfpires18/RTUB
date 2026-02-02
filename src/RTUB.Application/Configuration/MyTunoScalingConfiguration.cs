namespace RTUB.Application.Configuration;

public class MyTunoScalingConfiguration
{
    public const string SectionName = "myTuno";

    public MyTunoBaseStats BaseStats { get; set; } = new();
    public MyTunoLevelScaling LevelScaling { get; set; } = new();
    public MyTunoUpgrades Upgrades { get; set; } = new();
    public List<decimal> LevelCosts { get; set; } = new();
    public BattleRewards BattleRewards { get; set; } = new();
    
    /// <summary>
    /// Chance of beer drop after winning a battle (0.0 to 1.0)
    /// Default is 0.5 (50% chance)
    /// </summary>
    public double BeerDropChance { get; set; } = 0.5;
    public StageModeConfig StageMode { get; set; } = new();
}

public class BattleRewards
{
    public decimal WinReward { get; set; } = 10m;
    public decimal DrawReward { get; set; } = 7.5m;
    
    /// <summary>
    /// XP scaling based on level difference
    /// Formula: XP = BaseXP * (1.0 + (defenderLevel - attackerLevel) * XpScalingFactor)
    /// </summary>
    public double XpScalingFactor { get; set; } = 0.05; // 5% per level difference
    
    /// <summary>
    /// Minimum XP multiplier (prevents too little XP from weak opponents)
    /// Default 0.2 means minimum 20% of base XP
    /// </summary>
    public double MinXpMultiplier { get; set; } = 0.2;
    
    /// <summary>
    /// Maximum XP multiplier (prevents too much XP from strong opponents)
    /// Default 3.0 means maximum 300% of base XP
    /// </summary>
    public double MaxXpMultiplier { get; set; } = 3.0;
    
    /// <summary>
    /// Cost in Fidelis to revive a defeated character
    /// Default is 100 Fidelis
    /// </summary>
    public decimal ReviveCost { get; set; } = 100m;
}

public class MyTunoBaseStats
{
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public int HP { get; set; } = 100;
    public int Power { get; set; } = 10;
    public int Speed { get; set; } = 10;
    public double CriticalChance { get; set; } = 0.01;
}

public class MyTunoLevelScaling
{
    public double StatMultiplierPerLevel { get; set; } = 0.1;
    public int XpPerLevelBase { get; set; } = 100;
    public string XpToNextLevelFormula { get; set; } = "level * 100";
}

public class MyTunoUpgrades
{
    public MyTunoUpgradeStat HP { get; set; } = new();
    public MyTunoUpgradeStat Power { get; set; } = new();
    public MyTunoUpgradeStat Speed { get; set; } = new();
    public MyTunoUpgradeStat CriticalChance { get; set; } = new();
}

public class MyTunoUpgradeStat
{
    public int InitialBought { get; set; }
    public double BonusPerUpgrade { get; set; }
    public decimal BaseCost { get; set; }
}

/// <summary>
/// Configuration for Stage Mode
/// </summary>
public class StageModeConfig
{
    /// <summary>
    /// Number of enemies per stage formula base
    /// Formula: 1 + (stage / EnemyCountStageInterval)
    /// </summary>
    public int EnemyCountStageInterval { get; set; } = 20;

    /// <summary>
    /// Maximum number of enemies per stage
    /// </summary>
    public int MaxEnemiesPerStage { get; set; } = 5;

    /// <summary>
    /// Base XP reward for clearing a stage
    /// </summary>
    public int BaseStageXP { get; set; } = 30;

    /// <summary>
    /// XP multiplier for mini-boss stages
    /// </summary>
    public int MiniBossXPMultiplier { get; set; } = 3;

    /// <summary>
    /// XP multiplier for boss stages
    /// </summary>
    public int BossXPMultiplier { get; set; } = 10;

    /// <summary>
    /// Drop rates for items
    /// </summary>
    public StageDropRates DropRates { get; set; } = new();

    /// <summary>
    /// Region configurations
    /// </summary>
    public List<RegionConfig> Regions { get; set; } = new();

    /// <summary>
    /// Default enemy sprite if no region config exists
    /// </summary>
    public string DefaultEnemySprite { get; set; } = "/sprites/games/my-tuno/default_enemy.png";

    /// <summary>
    /// Default background if no region config exists
    /// </summary>
    public string DefaultBackground { get; set; } = "/sprites/games/my-tuno/backgrounds/forest.png";

    /// <summary>
    /// Player sprite for stage mode
    /// </summary>
    public string PlayerSprite { get; set; } = "/sprites/games/my-tuno/default_tuno.png";
}

/// <summary>
/// Drop rates for stage mode
/// </summary>
public class StageDropRates
{
    public double BeerDropChance { get; set; } = 0.10;
    public double ShotDropChance { get; set; } = 0.05;
    public double BossDropMultiplier { get; set; } = 3.0;
    public double MiniBossDropMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Configuration for a region/biome
/// </summary>
public class RegionConfig
{
    /// <summary>
    /// Region identifier (matches RegionType enum)
    /// </summary>
    public string RegionId { get; set; } = "Forest";

    /// <summary>
    /// Display name for the region
    /// </summary>
    public string DisplayName { get; set; } = "Forest";

    /// <summary>
    /// Background image path
    /// </summary>
    public string BackgroundSprite { get; set; } = "/sprites/games/my-tuno/backgrounds/forest.png";

    /// <summary>
    /// List of enemy sprites for this region
    /// </summary>
    public List<string> EnemySprites { get; set; } = new();

    /// <summary>
    /// Boss sprite for this region
    /// </summary>
    public string BossSprite { get; set; } = "/sprites/games/my-tuno/enemies/boss_default.png";

    /// <summary>
    /// Mini-boss sprite for this region
    /// </summary>
    public string MiniBossSprite { get; set; } = "/sprites/games/my-tuno/enemies/miniboss_default.png";
}
