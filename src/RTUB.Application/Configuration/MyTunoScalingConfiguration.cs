namespace RTUB.Application.Configuration;

public class MyTunoScalingConfiguration
{
    public const string SectionName = "myTuno";

    /// <summary>
    /// Game version string displayed in the UI
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Game description displayed in the UI (Portuguese)
    /// </summary>
    public string Description { get; set; } = "Luta contra oponentes AI e melhora o teu personagem através de combates estratégicos.";

    /// <summary>
    /// Upcoming features text displayed in the UI (Portuguese)
    /// </summary>
    public string NextFeatures { get; set; } = "Novos modos de jogo, equipamento personalizável, e torneios entre jogadores.";

    public MyTunoBaseStats BaseStats { get; set; } = new();
    public MyTunoLevelScaling LevelScaling { get; set; } = new();
    public MyTunoUpgrades Upgrades { get; set; } = new();
    public BattleRewards BattleRewards { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public ItemConfig Items { get; set; } = new();
    public MatchmakingConfig Matchmaking { get; set; } = new();

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

    /// <summary>
    /// Configuration for the Destilaria (resource gathering) system
    /// </summary>
    public GatheringConfig Gathering { get; set; } = new();
}

public class BattleRewards
{
    public decimal WinReward { get; set; } = 10m;
    public decimal DrawReward { get; set; } = 7.5m;

    /// <summary>
    /// Base XP reward for winning an arena battle
    /// </summary>
    public int BaseWinXP { get; set; } = 50;

    /// <summary>
    /// Base XP reward for drawing an arena battle
    /// </summary>
    public int BaseDrawXP { get; set; } = 30;

    /// <summary>
    /// XP scaling based on level difference between attacker and defender.
    /// Applied after attacker level scaling.
    /// Formula: levelDiffMultiplier = clamp(1.0 + levelDiff * XpScalingFactor, Min, Max)
    /// </summary>
    public double XpScalingFactor { get; set; } = 0.05; // 5% per level difference

    /// <summary>
    /// Minimum level-difference XP multiplier (prevents too little XP from weak opponents)
    /// Default 0.2 means minimum 20% of level-scaled XP
    /// </summary>
    public double MinXpMultiplier { get; set; } = 0.2;

    /// <summary>
    /// Maximum level-difference XP multiplier (prevents too much XP from strong opponents)
    /// Default 3.0 means maximum 300% of level-scaled XP
    /// </summary>
    public double MaxXpMultiplier { get; set; } = 3.0;

    /// <summary>
    /// Attacker level scaling factor for XP rewards.
    /// Scales base XP by attacker level so higher-level players earn proportionally more XP.
    /// Formula: effectiveBaseXP = BaseXP * (1 + attackerLevel * AttackerLevelXpScale)
    /// Default 0.08 means +8% per attacker level (level 100 → 9x base XP).
    /// </summary>
    public double AttackerLevelXpScale { get; set; } = 0.08;

    /// <summary>
    /// Attacker level scaling factor for Fidelis rewards.
    /// Scales base Fidelis by attacker level so higher-level players earn proportionally more.
    /// Formula: effectiveBaseFidelis = BaseFidelis * (1 + attackerLevel * AttackerLevelFidelisScale)
    /// Default 0.06 means +6% per attacker level (level 100 → 7x base Fidelis).
    /// </summary>
    public double AttackerLevelFidelisScale { get; set; } = 0.06;

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

    /// <summary>
    /// Fidelis reward multiplier per enemy level above 1
    /// Formula: fidelis = baseFidelis * (1.0 + (level - 1) * FidelisLevelMultiplier)
    /// </summary>
    public double FidelisLevelMultiplier { get; set; } = 0.1;
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
    public double StatGrowthExponent { get; set; } = 0.0;
    public int XpPerLevelBase { get; set; } = 100;
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
    public int MaxUpgrades { get; set; } = 50;
}

/// <summary>
/// Configuration for Stage Mode
/// </summary>
public class StageModeConfig
{
    /// <summary>
    /// Base XP reward for clearing a stage
    /// </summary>
    public int BaseStageXP { get; set; } = 30;

    /// <summary>
    /// XP multiplier for boss stages
    /// </summary>
    public int BossXPMultiplier { get; set; } = 10;

    /// <summary>
    /// Stage reward scaling rate for diminishing returns curve.
    /// Formula: stageScaling = 1.0 + (MaxStageRewardMultiplier - 1) * (1 - e^(-stageNumber * StageRewardScalingFactor))
    /// Default 0.005 gives a smooth curve approaching MaxStageRewardMultiplier.
    /// </summary>
    public double StageRewardScalingFactor { get; set; } = 0.005;

    /// <summary>
    /// Maximum reward multiplier from stage progression (asymptotic cap).
    /// The diminishing returns curve approaches this value but never exceeds it.
    /// Default 8.0 means rewards plateau at ~8x base at very high stages.
    /// </summary>
    public double MaxStageRewardMultiplier { get; set; } = 8.0;

    /// <summary>
    /// Maximum Fidelis level multiplier cap.
    /// Limits the effect of character level on Fidelis rewards.
    /// Formula: levelMultiplier = min(1 + (level-1) * FidelisLevelMultiplier, FidelisLevelMultiplierCap)
    /// Default 5.0 caps at 5x regardless of level.
    /// </summary>
    public double FidelisLevelMultiplierCap { get; set; } = 5.0;

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
    /// Stat bonuses for equipped items (equipment + instruments)
    /// </summary>
    public EquipmentStatsConfig EquipmentStats { get; set; } = new();

    /// <summary>
    /// Fidelis values for discarding items
    /// </summary>
    public DiscardValuesConfig DiscardValues { get; set; } = new();

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
    public decimal BossWin { get; set; } = 50m;
}

/// <summary>
/// Drop rates for stage mode
/// </summary>
public class StageDropRates
{
    public double BeerDropChance { get; set; } = 0.10;
    public double ShotDropChance { get; set; } = 0.05;
    public double InstrumentPartDropChance { get; set; } = 0.005;
    public double EquipmentDropChance { get; set; } = 0.008;
    public double BossDropMultiplier { get; set; } = 3.0;
    public List<EquipmentDropConfig> EquipmentDrops { get; set; } = new();
}

/// <summary>
/// Per-slot configuration for equipment drops, including optional custom sprite.
/// </summary>
public class EquipmentDropConfig
{
    public string Slot { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SpritePath { get; set; }
}

/// <summary>
/// Stat bonuses granted by each equipped piece.
/// </summary>
public class EquipmentPieceStats
{
    public int HP { get; set; }
    public int Power { get; set; }
    public int Speed { get; set; }
    public int Defense { get; set; }
    public double CriticalChance { get; set; }
}

/// <summary>
/// Configuration for equipment stat bonuses by slot.
/// </summary>
public class EquipmentStatsConfig
{
    public EquipmentPieceStats Head { get; set; } = new() { HP = 15, Defense = 3 };
    public EquipmentPieceStats Shoulders { get; set; } = new() { HP = 10, Defense = 5 };
    public EquipmentPieceStats Chest { get; set; } = new() { HP = 25, Defense = 8 };
    public EquipmentPieceStats Gloves { get; set; } = new() { Power = 5, CriticalChance = 0.01 };
    public EquipmentPieceStats Legs { get; set; } = new() { HP = 15, Speed = 2, Defense = 3 };
    public EquipmentPieceStats Boots { get; set; } = new() { Speed = 4, Defense = 2 };
    public EquipmentPieceStats Instrument { get; set; } = new() { Power = 8, CriticalChance = 0.005 };
}

/// <summary>
/// Fidelis values for discarding items from inventory.
/// </summary>
public class DiscardValuesConfig
{
    /// <summary>Fidelis gained from discarding an equipment piece.</summary>
    public decimal Equipment { get; set; } = 25m;

    /// <summary>Fidelis gained from discarding an instrument part.</summary>
    public decimal InstrumentPart { get; set; } = 30m;
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

    /// <summary>
    /// Difficulty multiplier applied to all enemy stats in this biome.
    /// 1.0 = default (Forest), higher values make enemies tougher.
    /// Applied on top of the per-stage scaling.
    /// </summary>
    public double DifficultyMultiplier { get; set; } = 1.0;
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

/// <summary>
/// Combat engine configuration for battle simulation
/// </summary>
public class CombatConfig
{
    /// <summary>
    /// Maximum critical chance cap for enemies (0.5 = 50%)
    /// </summary>
    public double CriticalChanceCap { get; set; } = 0.5;
}

/// <summary>
/// Configuration for consumable items (beer, shots)
/// </summary>
public class ItemConfig
{
    /// <summary>
    /// Shot buff stat multiplier (1.20 = 20% boost to all combat stats)
    /// </summary>
    public double ShotBuffMultiplier { get; set; } = 1.20;
}

/// <summary>
/// Matchmaking configuration for arena battles
/// </summary>
public class MatchmakingConfig
{
    /// <summary>
    /// Cooldown in minutes before fighting the same opponent again
    /// </summary>
    public int CooldownMinutes { get; set; } = 60;

    /// <summary>
    /// Initial power range lower bound (0.7 = 70% of player power)
    /// </summary>
    public double InitialPowerRangeMin { get; set; } = 0.7;

    /// <summary>
    /// Initial power range upper bound (1.3 = 130% of player power)
    /// </summary>
    public double InitialPowerRangeMax { get; set; } = 1.3;

    /// <summary>
    /// Expanded power range lower bound when initial search fails (0.5 = 50%)
    /// </summary>
    public double ExpandedPowerRangeMin { get; set; } = 0.5;

    /// <summary>
    /// Expanded power range upper bound when initial search fails (1.5 = 150%)
    /// </summary>
    public double ExpandedPowerRangeMax { get; set; } = 1.5;

    /// <summary>
    /// Weights for power rating calculation
    /// </summary>
    public PowerRatingWeights PowerRatingWeights { get; set; } = new();
}

/// <summary>
/// Weights for the power rating calculation formula
/// Formula: (HP * HpWeight) + (Power * PowerWeight) + (Speed * SpeedWeight)
/// </summary>
public class PowerRatingWeights
{
    public double Hp { get; set; } = 0.5;
    public double Power { get; set; } = 2.0;
    public double Speed { get; set; } = 1.5;
}

/// <summary>
/// Configuration for the Destilaria (resource gathering) system
/// </summary>
public class GatheringConfig
{
    /// <summary>
    /// Maximum energy capacity for gathering
    /// </summary>
    public int MaxEnergy { get; set; } = 10;

    /// <summary>
    /// Seconds between each energy regeneration tick (1 energy per interval)
    /// Default is 60 (1 energy per minute)
    /// </summary>
    public int RegenIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Cast time in seconds before a resource is gathered
    /// </summary>
    public int CastTimeSeconds { get; set; } = 3;

    /// <summary>
    /// List of available gathering resources
    /// </summary>
    public List<GatheringResourceConfig> Resources { get; set; } = new();
}

/// <summary>
/// Configuration for a single gathering resource
/// </summary>
public class GatheringResourceConfig
{
    /// <summary>
    /// The InventoryItemType name (e.g., "Vodka", "Gin")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the resource
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Energy cost to gather this resource
    /// </summary>
    public int EnergyCost { get; set; } = 1;

    /// <summary>
    /// Bootstrap icon class (e.g., "bi-droplet") — used as fallback if no sprite
    /// </summary>
    public string Icon { get; set; } = "bi-box";

    /// <summary>
    /// Path to the sprite image relative to wwwroot (e.g., "sprites/games/my-tuno/drinks/vodka.png")
    /// If empty, the bootstrap icon is used instead
    /// </summary>
    public string SpritePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this resource is currently available for gathering
    /// </summary>
    public bool IsActive { get; set; } = true;
}
