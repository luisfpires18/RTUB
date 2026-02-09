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

    /// <summary>
    /// Enemy speed scaling rate relative to StatMultiplierPerLevel.
    /// Controls how fast enemies' speed grows per stage.
    /// A value of 0.073 with StatMultiplierPerLevel=0.15 makes enemies reach 1.0s action time around stage 900.
    /// Default is 0.5 (original half-rate scaling).
    /// </summary>
    public double EnemySpeedScalingRate { get; set; } = 0.5;

    public StageModeConfig StageMode { get; set; } = new();

    /// <summary>
    /// Configuration for the Destilaria (resource gathering) system
    /// </summary>
    public GatheringConfig Gathering { get; set; } = new();

    /// <summary>
    /// Daily reward configuration
    /// </summary>
    public DailyRewardConfig DailyReward { get; set; } = new();
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
    /// Unified level-difference scaling for both XP and Fidelis arena rewards.
    /// Formula: rewardMultiplier = clamp(1.0 + (defenderLevel - attackerLevel) * LevelDiffScale, Min, Max)
    /// Beating higher level = bonus, beating lower level = penalty, 20+ levels above = zero.
    /// Default 0.05 = 5% per level difference.
    /// </summary>
    public double LevelDiffScale { get; set; } = 0.05;

    /// <summary>
    /// Minimum reward multiplier. 0.0 means beating someone 20+ levels below gives nothing.
    /// </summary>
    public double MinRewardMultiplier { get; set; } = 0.0;

    /// <summary>
    /// Maximum reward multiplier (cap for beating much stronger opponents).
    /// Default 3.0 = max 300% rewards.
    /// </summary>
    public double MaxRewardMultiplier { get; set; } = 3.0;

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
    public double CriticalChance { get; set; } = 0.0;
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
    public double MultiplierPerUpgrade { get; set; }
    public decimal BaseCost { get; set; }
    public double CostExponent { get; set; } = 1.5;
    public int MaxUpgrades { get; set; } = 50;
}

/// <summary>
/// Configuration for Stage Mode
/// </summary>
public class StageModeConfig
{
    /// <summary>
    /// Base XP reward for clearing a stage (legacy, used as fallback)
    /// </summary>
    public int BaseStageXP { get; set; } = 60;

    /// <summary>
    /// XP base per enemy level. XP = xpPerEnemyLevel × enemyLevel^enemyLevelXPPower × enemyCount × levelDiffMult.
    /// Enemy level = stage number. Higher-level enemies give more XP.
    /// </summary>
    public double XpPerEnemyLevel { get; set; } = 12;

    /// <summary>
    /// Power applied to enemy level for XP scaling.
    /// 0.5 = sqrt (stage 100 gives 10× stage 1), 1.0 = linear (stage 100 gives 100× stage 1).
    /// </summary>
    public double EnemyLevelXPPower { get; set; } = 0.5;

    /// <summary>
    /// XP penalty rate per level above enemy. When playerLevel > stageNumber,
    /// XP is multiplied by max(MinXPLevelMultiplier, 1.0 - (playerLevel - stageNumber) × XpLevelPenaltyRate).
    /// 0.015 means 1.5% reduction per level above enemy.
    /// </summary>
    public double XpLevelPenaltyRate { get; set; } = 0.015;

    /// <summary>
    /// Minimum XP multiplier floor when player is much higher level than enemies.
    /// 0.05 = enemies always give at least 5% XP regardless of level difference.
    /// </summary>
    public double MinXPLevelMultiplier { get; set; } = 0.05;

    /// <summary>
    /// XP multiplier for boss stages
    /// </summary>
    public int BossXPMultiplier { get; set; } = 8;

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
    /// Per-level Fidelis multiplier for stage rewards.
    /// Formula: levelMultiplier = min(1 + (level-1) * FidelisLevelMultiplier, FidelisLevelMultiplierCap)
    /// Default 0.04 = 4% per level.
    /// </summary>
    public double FidelisLevelMultiplier { get; set; } = 0.04;

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
    /// Minimum quality multiplier for equipment pieces (randomized per character per slot).
    /// Default 0.7 = worst quality gets 70% of base stats.
    /// </summary>
    public double EquipmentQualityMin { get; set; } = 0.7;

    /// <summary>
    /// Maximum quality multiplier for equipment pieces (randomized per character per slot).
    /// Default 1.3 = best quality gets 130% of base stats.
    /// </summary>
    public double EquipmentQualityMax { get; set; } = 1.3;

    /// <summary>
    /// Stat bonuses for equipped items (equipment + instruments)
    /// </summary>
    public EquipmentStatsConfig EquipmentStats { get; set; } = new();

    /// <summary>
    /// Fidelis values for discarding items
    /// </summary>
    public DiscardValuesConfig DiscardValues { get; set; } = new();

    /// <summary>
    /// Forging configuration (cast time, etc.)
    /// </summary>
    public ForgingConfig Forging { get; set; } = new();

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
    public EquipmentPieceStats Gloves { get; set; } = new() { Power = 5 };
    public EquipmentPieceStats Legs { get; set; } = new() { HP = 15, Defense = 3 };
    public EquipmentPieceStats Boots { get; set; } = new() { HP = 5, Defense = 2 };
    public EquipmentPieceStats Instrument { get; set; } = new() { Power = 8 };
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
/// Forging configuration for weapon crafting
/// </summary>
public class ForgingConfig
{
    /// <summary>Cast time in seconds for forging a weapon.</summary>
    public int CastTimeSeconds { get; set; } = 5;

    /// <summary>Base Fidelis cost to upgrade a weapon from level 0 to 1.</summary>
    public decimal WeaponUpgradeBaseCost { get; set; } = 50m;

    /// <summary>Cost multiplier per level: cost = BaseCost * (Multiplier ^ currentLevel).</summary>
    public decimal WeaponUpgradeCostMultiplier { get; set; } = 1.5m;

    /// <summary>Stat increase percentage per weapon level (0.15 = +15% per level).</summary>
    public double WeaponUpgradeStatBonus { get; set; } = 0.15;

    /// <summary>
    /// Stat multiplier for two-handed weapons. Since 2H occupies both weapon slots,
    /// they get this multiplier on all stats to match dual-wielding 1H weapons.
    /// Default 2.0 = same total power as equipping two 1H weapons.
    /// </summary>
    public double TwoHandedMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Minimum quality multiplier for instrument-based weapons.
    /// Randomized between min/max at forge time for stat variety.
    /// Default 0.85 = worst quality gets 85% of base stats.
    /// </summary>
    public double InstrumentQualityMin { get; set; } = 0.85;

    /// <summary>
    /// Maximum quality multiplier for instrument-based weapons.
    /// Default 1.15 = best quality gets 115% of base stats.
    /// </summary>
    public double InstrumentQualityMax { get; set; } = 1.15;
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

    /// <summary>
    /// The minimum stage the player must have reached to unlock this drink.
    /// 1 = available from the start. Matches the stageMin of the corresponding biome.
    /// </summary>
    public int UnlockStage { get; set; } = 1;
}

/// <summary>
/// Configuration for daily login rewards
/// </summary>
public class DailyRewardConfig
{
    /// <summary>Base Fidelis amount for the daily reward.</summary>
    public decimal BaseFidelis { get; set; } = 15m;

    /// <summary>Additional Fidelis per character level.</summary>
    public decimal PerLevelFidelis { get; set; } = 2m;
}
