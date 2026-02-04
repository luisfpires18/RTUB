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

    /// <summary>
    /// Defense constant K for damage mitigation formula: mult = K / (K + defense)
    /// Higher K means defense is less effective (more damage taken)
    /// Default is 50
    /// </summary>
    public double DefenseK { get; set; } = 50;

    /// <summary>
    /// Minimum damage that can be dealt after defense mitigation
    /// Default is 1
    /// </summary>
    public int MinDamage { get; set; } = 1;

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

    /// <summary>
    /// Cost in Fidelis to restore HP to maximum
    /// Default is 50 Fidelis
    /// </summary>
    public decimal RestoreHPCost { get; set; } = 50m;
}

public class MyTunoBaseStats
{
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public int HP { get; set; } = 100;
    public int Power { get; set; } = 10;
    public int Speed { get; set; } = 10;
    public int Defense { get; set; } = 5;
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
    public MyTunoUpgradeStat Defense { get; set; } = new();
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
    /// Enemy stat scaling per stage
    /// </summary>
    public EnemyScaling EnemyScaling { get; set; } = new();

    /// <summary>
    /// Base enemy stats by type
    /// </summary>
    public BaseEnemyStats BaseEnemyStats { get; set; } = new();

    /// <summary>
    /// Fidelis rewards by enemy type
    /// </summary>
    public FidelisRewards FidelisRewards { get; set; } = new();

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

    /// <summary>
    /// Calculates the number of enemies for a given stage
    /// </summary>
    public int GetEnemyCount(int stageNumber)
    {
        var count = 1 + (stageNumber / EnemyCountStageInterval);
        return Math.Min(count, MaxEnemiesPerStage);
    }

    /// <summary>
    /// Biome configurations for infinite stage progression
    /// </summary>
    public List<BiomeConfig> Biomes { get; set; } = new();

    /// <summary>
    /// Encounter rules for stage progression
    /// </summary>
    public EncounterRulesConfig EncounterRules { get; set; } = new();

    /// <summary>
    /// Stat scaling configuration
    /// </summary>
    public StageScalingConfig Scaling { get; set; } = new();
}

/// <summary>
/// Enemy stat scaling per stage
/// </summary>
public class EnemyScaling
{
    /// <summary>
    /// HP increase per stage as a percentage (0.08 = 8%)
    /// </summary>
    public double HpPerStage { get; set; } = 0.08;

    /// <summary>
    /// Power increase per stage as a percentage
    /// </summary>
    public double PowerPerStage { get; set; } = 0.05;

    /// <summary>
    /// Speed increase per stage as a percentage
    /// </summary>
    public double SpeedPerStage { get; set; } = 0.03;

    /// <summary>
    /// Defense increase per stage as a percentage
    /// </summary>
    public double DefensePerStage { get; set; } = 0.04;

    /// <summary>
    /// Critical chance increase per stage
    /// </summary>
    public double CriticalChancePerStage { get; set; } = 0.002;
}

/// <summary>
/// Base enemy stats by type
/// </summary>
public class BaseEnemyStats
{
    public EnemyTypeStat Normal { get; set; } = new() { Hp = 50, Power = 8, Speed = 5, Defense = 3, CriticalChance = 0.05 };
    public EnemyTypeStat MiniBoss { get; set; } = new() { Hp = 200, Power = 15, Speed = 7, Defense = 8, CriticalChance = 0.10 };
    public EnemyTypeStat Boss { get; set; } = new() { Hp = 500, Power = 20, Speed = 8, Defense = 15, CriticalChance = 0.15 };
}

/// <summary>
/// Stats for a single enemy type
/// </summary>
public class EnemyTypeStat
{
    public int Hp { get; set; }
    public int Power { get; set; }
    public int Speed { get; set; }
    public int Defense { get; set; }
    public double CriticalChance { get; set; }
}

/// <summary>
/// Fidelis rewards by enemy type
/// </summary>
public class FidelisRewards
{
    public decimal NormalWin { get; set; } = 10m;
    public decimal MiniBossWin { get; set; } = 25m;
    public decimal BossWin { get; set; } = 50m;
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

/// <summary>
/// Biome configuration for infinite stage progression
/// </summary>
public class BiomeConfig
{
    /// <summary>
    /// Biome name (e.g., "Forest", "Desert")
    /// </summary>
    public string Name { get; set; } = "Forest";

    /// <summary>
    /// Minimum stage number for this biome (inclusive)
    /// </summary>
    public int StageMin { get; set; } = 1;

    /// <summary>
    /// Maximum stage number for this biome (inclusive)
    /// </summary>
    public int StageMax { get; set; } = 100;

    /// <summary>
    /// Path to enemy sprite folder (relative to wwwroot)
    /// </summary>
    public string EnemySpritePath { get; set; } = "sprites/games/my-tuno/enemies/forest";

    /// <summary>
    /// Prefix for boss sprite filenames (e.g., "boss_")
    /// </summary>
    public string BossSpritePrefix { get; set; } = "boss_";
}

/// <summary>
/// Encounter rules configuration for stage progression
/// </summary>
public class EncounterRulesConfig
{
    /// <summary>
    /// Boss appears every N stages (e.g., 10 = boss on stage 10, 20, 30, etc.)
    /// </summary>
    public int BossEveryNStages { get; set; } = 10;

    /// <summary>
    /// Enemy count rules based on stage offset within each "decade"
    /// Offset = (stage - 1) % BossEveryNStages + 1
    /// </summary>
    public List<EnemyCountRule> EnemyCountByStageOffset { get; set; } = new();
}

/// <summary>
/// Enemy count rule for a range of stage offsets
/// </summary>
public class EnemyCountRule
{
    /// <summary>
    /// Starting stage offset (inclusive)
    /// </summary>
    public int From { get; set; }

    /// <summary>
    /// Ending stage offset (inclusive)
    /// </summary>
    public int To { get; set; }

    /// <summary>
    /// Number of enemies to spawn
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Stat scaling configuration for infinite stage progression
/// </summary>
public class StageScalingConfig
{
    /// <summary>
    /// HP growth rate per stage (0.06 = 6% per stage)
    /// </summary>
    public double HpGrowthPerStage { get; set; } = 0.06;

    /// <summary>
    /// Damage growth rate per stage (0.05 = 5% per stage)
    /// </summary>
    public double DamageGrowthPerStage { get; set; } = 0.05;

    /// <summary>
    /// Armor growth rate per stage (0.03 = 3% per stage)
    /// </summary>
    public double ArmorGrowthPerStage { get; set; } = 0.03;

    /// <summary>
    /// Boss stat multiplier (2.5 = boss has 2.5x stats of normal enemy)
    /// </summary>
    public double BossMultiplier { get; set; } = 2.5;
}
