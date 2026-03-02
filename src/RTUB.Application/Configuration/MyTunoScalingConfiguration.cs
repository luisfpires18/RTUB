namespace RTUB.Application.Configuration;

public class MyTunoScalingConfiguration
{
    public const string SectionName = "myTuno";

    /// <summary>
    /// Upcoming features text displayed in the UI (Portuguese)
    /// </summary>
    public string NextFeatures { get; set; } = "Novos modos de jogo, equipamento personalizável, e torneios entre jogadores.";

    public MyTunoBaseStats BaseStats { get; set; } = new();
    public MyTunoLevelScaling LevelScaling { get; set; } = new();
    public MyTunoUpgrades Upgrades { get; set; } = new();
    public MyTunoImprovements Improvements { get; set; } = new();
    public BattleRewards BattleRewards { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();

    /// <summary>
    /// Consumable item effect configuration (heal amounts, cooldowns, charges, buffs).
    /// </summary>
    public ConsumablesConfig Consumables { get; set; } = new();

    /// <summary>
    /// Consumable upgrade rank configuration (costs and max ranks for consumable upgrades).
    /// </summary>
    public ConsumableUpgradesConfig ConsumableUpgrades { get; set; } = new();

    public StageModeConfig StageMode { get; set; } = new();

    /// <summary>
    /// Configuration for Boss Mode - endless boss-only mode requiring FITAB to enter
    /// </summary>
    public BossModeConfig BossMode { get; set; } = new();

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
    public decimal WinReward { get; set; } = 120m;
    public decimal DrawReward { get; set; } = 40m;

    /// <summary>
    /// Base XP reward for winning an arena battle
    /// </summary>
    public int BaseWinXP { get; set; } = 100;

    /// <summary>
    /// Base XP reward for drawing an arena battle
    /// </summary>
    public int BaseDrawXP { get; set; } = 50;

    /// <summary>
    /// Unified level-difference scaling for both XP and Fidelis arena rewards.
    /// Formula: rewardMultiplier = clamp(1.0 + (defenderLevel - attackerLevel) * LevelDiffScale, 1-Cap, 1+Cap)
    /// </summary>
    public double LevelDiffScale { get; set; } = 0.01;

    /// <summary>
    /// Maximum level difference bonus/penalty cap (0.5 = ±50%).
    /// </summary>
    public double LevelDiffCap { get; set; } = 0.5;

    /// <summary>
    /// Cost in Fidelis to revive a defeated character
    /// </summary>
    public decimal ReviveCost { get; set; } = 30m;

    /// <summary>
    /// Cost in Fidelis to restore HP to maximum
    /// </summary>
    public decimal RestoreHPCost { get; set; } = 20m;

    /// <summary>
    /// Chance of Fino drop after winning an arena battle (0.0 to 1.0)
    /// </summary>
    public double FinoDropChance { get; set; } = 0.0015;

    /// <summary>
    /// Chance of Caneca drop after winning an arena battle (0.0 to 1.0)
    /// </summary>
    public double CanecaDropChance { get; set; } = 0.0004;

    /// <summary>
    /// Chance of Cigarro drop after winning an arena battle
    /// </summary>
    public double CigarroDropChance { get; set; } = 0.0009;

    /// <summary>
    /// Chance of Canhão drop after winning an arena battle
    /// </summary>
    public double CanhaoDropChance { get; set; } = 0.0003;

    /// <summary>
    /// Chance of Penalty drop after winning an arena battle
    /// </summary>
    public double PenaltyDropChance { get; set; } = 0.0002;

    /// <summary>
    /// Chance of Shot drop after winning an arena battle
    /// </summary>
    public double ShotDropChance { get; set; } = 0.0009;
}

public class MyTunoBaseStats
{
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public int HP { get; set; } = 200;
    public int Power { get; set; } = 25;
    public int Speed { get; set; } = 10;
    public int Defense { get; set; } = 20;
    public double CriticalChance { get; set; } = 0.02;
}

public class MyTunoLevelScaling
{
    /// <summary>
    /// Maximum player level. After this, XP still accumulates but no more level-ups.
    /// </summary>
    public int MaxLevel { get; set; } = 100;

    /// <summary>
    /// Linear bonus per level: LevelFactor = 1 + BonusPerLevel * (Level - 1).
    /// At max level 100: factor = 1 + 0.008 * 99 = 1.792.
    /// </summary>
    public double BonusPerLevel { get; set; } = 0.008;

    public int XpPerLevelBase { get; set; } = 100;

    /// <summary>
    /// Exponent for XP growth: XP needed = XpPerLevelBase * Level^XpGrowthExponent.
    /// With 1.5, level 100 needs ~100,000 XP.
    /// </summary>
    public double XpGrowthExponent { get; set; } = 1.5;
}

public class MyTunoUpgrades
{
    public UpgradeLogStat HP { get; set; } = new() { FlatBonus = 100, BaseCost = 50, CostPerLevel = 50 };
    public UpgradeLogStat Power { get; set; } = new() { FlatBonus = 15, BaseCost = 50, CostPerLevel = 50 };
    public UpgradeFlatStat Speed { get; set; } = new() { FlatBonus = 1.5, BaseCost = 150, CostPerLevel = 150, MaxUpgrades = 41 };
    public UpgradeFlatStat CriticalChance { get; set; } = new() { FlatBonus = 0.005, BaseCost = 120, CostPerLevel = 120, MaxUpgrades = 80 };
    public UpgradeLogStat Defense { get; set; } = new() { FlatBonus = 12, BaseCost = 50, CostPerLevel = 50 };
}

/// <summary>
/// Upgrade stat using flat additive scaling: totalStat = (base + flatBonus * n) * levelFactor.
/// Cost is linear: baseCost + n * costPerLevel.
/// </summary>
public class UpgradeLogStat
{
    /// <summary>Flat bonus added per upgrade: totalStat = (base + flatBonus * n) * levelFactor.</summary>
    public double FlatBonus { get; set; }

    /// <summary>Base Fidelis cost for the first upgrade.</summary>
    public decimal BaseCost { get; set; }

    /// <summary>Cost increment per level: cost(n) = baseCost + n * costPerLevel.</summary>
    public decimal CostPerLevel { get; set; }

    /// <summary>Maximum upgrades allowed. 0 = unlimited.</summary>
    public int MaxUpgrades { get; set; } = 0;
}

/// <summary>
/// Upgrade stat using flat additive bonus per upgrade.
/// Cost is linear: baseCost + n * costPerLevel.
/// </summary>
public class UpgradeFlatStat
{
    /// <summary>Flat bonus added per upgrade (e.g., 0.005 crit chance per upgrade).</summary>
    public double FlatBonus { get; set; }

    /// <summary>Base Fidelis cost for the first upgrade.</summary>
    public decimal BaseCost { get; set; }

    /// <summary>Cost increment per level: cost(n) = baseCost + n * costPerLevel.</summary>
    public decimal CostPerLevel { get; set; }

    /// <summary>Maximum upgrades allowed. 0 = unlimited.</summary>
    public int MaxUpgrades { get; set; } = 0;
}

/// <summary>
/// Configuration for Improvements (game-wide improvements).
/// Each improvement has its own cost scaling, separate from stat upgrades.
/// </summary>
public class MyTunoImprovements
{
    /// <summary>Increase maximum energy capacity</summary>
    public UpgradeFlatStat EnergyAmount { get; set; } = new() { FlatBonus = 2.0, BaseCost = 150, CostPerLevel = 150 };

    /// <summary>Increase energy regeneration speed</summary>
    public UpgradeFlatStat EnergyRegen { get; set; } = new() { FlatBonus = 2.0, BaseCost = 300, CostPerLevel = 300 };

    /// <summary>
    /// Reduce gathering cast time (doubling cost: cost = BaseCost × 2^level).
    /// FlatBonus = time reduction per upgrade in seconds.
    /// BaseCost = starting cost (500K). CostPerLevel is unused (doubling formula).
    /// </summary>
    public UpgradeFlatStat CastSpeed { get; set; } = new() { FlatBonus = 0.1, BaseCost = 500_000m, CostPerLevel = 0 };

    /// <summary>Minimum cast time in seconds (floor).</summary>
    public double MinCastTime { get; set; } = 0.1;

    /// <summary>
    /// Double gathering chance improvement (Destilaria).
    /// FlatBonus = chance increase per upgrade (0.02 = 2%).
    /// Uses doubling cost formula like CastSpeed.
    /// </summary>
    public UpgradeFlatStat DoubleGathering { get; set; } = new() { FlatBonus = 0.02, BaseCost = 200_000m, CostPerLevel = 0 };

    /// <summary>Maximum double gathering chance cap (0.50 = 50%).</summary>
    public double MaxDoubleGatheringChance { get; set; } = 0.50;

}

/// <summary>
/// Configuration for Stage Mode with tiered enemy system.
/// </summary>
public class StageModeConfig
{
    /// <summary>
    /// Boss stat multiplier applied to all enemy stats when the enemy is a boss.
    /// </summary>
    public double BossMultiplier { get; set; } = 1.8;

    /// <summary>
    /// Global buff multiplier applied to all stage enemy HP, Power, and Defense.
    /// 1.01 = enemies are 1% stronger than tier base values.
    /// </summary>
    public double EnemyStatBuff { get; set; } = 1.0;

    /// <summary>
    /// Drop rates for items
    /// </summary>
    public StageDropRates DropRates { get; set; } = new();

    /// <summary>
    /// Stat bonuses for equipped items (equipment + instruments)
    /// </summary>
    public EquipmentStatsConfig EquipmentStats { get; set; } = new();

    /// <summary>
    /// Flat HP bonus per equipment enhancement level (matches stat upgrade flatBonus).
    /// </summary>
    public int EquipmentHpPerLevel { get; set; } = 100;

    /// <summary>
    /// Flat Power bonus per equipment enhancement level (matches stat upgrade flatBonus).
    /// </summary>
    public int EquipmentPowerPerLevel { get; set; } = 15;

    /// <summary>
    /// Flat Defense bonus per equipment enhancement level (matches stat upgrade flatBonus).
    /// </summary>
    public int EquipmentDefensePerLevel { get; set; } = 12;

    /// <summary>
    /// Minimum quality multiplier for equipment pieces.
    /// Default 0.7 = worst quality gets 70% of base stats.
    /// </summary>
    public double EquipmentQualityMin { get; set; } = 0.7;

    /// <summary>
    /// Maximum quality multiplier for equipment pieces.
    /// Default 1.3 = best quality gets 130% of base stats.
    /// </summary>
    public double EquipmentQualityMax { get; set; } = 1.3;

    /// <summary>
    /// Bonus per equipment enhancement tier.
    /// Enhancement = floor(highestStage / 100). Formula: stat * (1 + enhancement * EquipmentEnhancementBonus).
    /// </summary>
    public double EquipmentEnhancementBonus { get; set; } = 0.05;

    /// <summary>
    /// Maximum equipment enhancement level.
    /// </summary>
    public int MaxEquipmentEnhancement { get; set; } = 9999;

    /// <summary>
    /// Per-character-level scaling factor applied to equipped weapon stat bonuses.
    /// Set to 0.0 in v5 to remove character-level-dependent equipment scaling.
    /// </summary>
    public double WeaponCharacterLevelScale { get; set; } = 0.0;

    /// <summary>
    /// Per-level scaling factor for equipment stat bonuses.
    /// Set to 0.0 in v5 to remove level-dependent equipment scaling.
    /// </summary>
    public double EquipmentLevelScale { get; set; } = 0.0;

    /// <summary>
    /// Per-level scaling factor for equipment discard Fidelis.
    /// </summary>
    public double DiscardLevelScale { get; set; } = 0.05;

    /// <summary>
    /// Fidelis values for discarding items
    /// </summary>
    public DiscardValuesConfig DiscardValues { get; set; } = new();

    /// <summary>
    /// Forging configuration (cast time, etc.)
    /// </summary>
    public ForgingConfig Forging { get; set; } = new();

    /// <summary>
    /// Tiered enemy stat tables. Each tier defines stats for a range of stages.
    /// Replaces the old polynomial difficulty curve with fixed, designer-tuned values.
    /// </summary>
    public List<StageEnemyTierConfig> EnemyTiers { get; set; } = new();

    /// <summary>
    /// Biome configurations for infinite stage progression
    /// </summary>
    public List<BiomeConfig> Biomes { get; set; } = new();

    /// <summary>
    /// Encounter rules for stage progression
    /// </summary>
    public EncounterRulesConfig EncounterRules { get; set; } = new();

    /// <summary>
    /// Rare set equipment configuration (ultra-rare drops that upgrade existing equipment).
    /// </summary>
    public RareSetConfig RareSet { get; set; } = new();

    /// <summary>
    /// Per-stage growth rate applied as a continuous multiplier on top of tier stats/rewards.
    /// Formula: multiplier = 1.0 + (stageNumber - 1) * PerStageGrowthRate.
    /// Default 0.001 = +0.1% per stage, so stage 1000 gets ~2.0×, stage 10000 gets ~11.0×.
    /// </summary>
    public double PerStageGrowthRate { get; set; } = 0.0015;
}

/// <summary>
/// Fixed enemy stat block for a tier of stages.
/// Replaces formula-based enemy generation with designer-tuned values.
/// </summary>
public class StageEnemyTierConfig
{
    /// <summary>Tier number (1-20).</summary>
    public int Tier { get; set; }

    /// <summary>First stage (inclusive) in this tier.</summary>
    public int MinStage { get; set; }

    /// <summary>Last stage (inclusive) in this tier.</summary>
    public int MaxStage { get; set; }

    // ── Normal enemy stats ──
    public int HP { get; set; }
    public int Power { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public double CritChance { get; set; }

    // ── Rewards ──
    public decimal FidelisReward { get; set; }
    public int XpReward { get; set; }

    // ── MiniBoss overrides (miniboss every 10 stages) ──
    public int MinibossHP { get; set; }
    public int MinibossPower { get; set; }
    public int MinibossDefense { get; set; }

    // ── Boss overrides (boss every 100 stages) ──
    public int BossHP { get; set; }
    public int BossPower { get; set; }
    public int BossDefense { get; set; }
}

/// <summary>
/// Drop rates for stage mode
/// </summary>
public class StageDropRates
{
    public double FinoDropChance { get; set; } = 0.0015;
    public double ShotDropChance { get; set; } = 0.0009;
    public double CigarroDropChance { get; set; } = 0.0009;
    public double CanecaDropChance { get; set; } = 0.0004;
    public double CanhaoDropChance { get; set; } = 0.0003;
    public double PenaltyDropChance { get; set; } = 0.0002;
    public double InstrumentPartDropChance { get; set; } = 0.0003;
    public double RareSetDropChance { get; set; } = 0.00005;
    public double BossDropMultiplier { get; set; } = 3.0;

    /// <summary>Leitão drop chance at stage-mode boss fights (only in the player's current biome).</summary>
    public double LeitaoDropChance { get; set; } = 0.33;
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
/// Configuration for the Rare Set equipment system.
/// Ultra-rare pieces drop in Stage Mode and upgrade existing equipment slots.
/// </summary>
public class RareSetConfig
{
    /// <summary>Crit chance bonus per applied rare piece (0.05 = 5%).</summary>
    public double CritBonusPerPiece { get; set; } = 0.05;

    /// <summary>Action time reduction per applied rare piece in seconds.</summary>
    public double SpeedReductionPerPiece { get; set; } = 0.05;

    /// <summary>Minimum rare pieces required to activate set bonus (replaces individual bonuses).</summary>
    public int SetBonusMinPieces { get; set; } = 6;

    /// <summary>Set bonus crit chance (replaces per-piece crit when active). 0.50 = 50%.</summary>
    public double SetBonusCrit { get; set; } = 0.50;

    /// <summary>Set bonus action time reduction in seconds (replaces per-piece speed when active).</summary>
    public double SetBonusSpeedReduction { get; set; } = 0.5;

    /// <summary>Per-slot configuration for each rare set piece.</summary>
    public Dictionary<string, RareSetPieceConfig> Pieces { get; set; } = new()
    {
        ["head"] = new() { Enabled = true, Name = "Rare Crown" },
        ["shoulders"] = new() { Enabled = true, Name = "Rare Mantle" },
        ["chest"] = new() { Enabled = true, Name = "Rare Vest" },
        ["gloves"] = new() { Enabled = true, Name = "Rare Gauntlets" },
        ["legs"] = new() { Enabled = true, Name = "Rare Greaves" },
        ["boots"] = new() { Enabled = true, Name = "Rare Treads" }
    };
}

/// <summary>
/// Configuration for an individual rare set piece.
/// </summary>
public class RareSetPieceConfig
{
    /// <summary>Whether this piece can drop. If false, it never drops and is excluded from the set.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Display name shown in inventory and equipment UI.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for equipment stat bonuses by slot.
/// </summary>
public class EquipmentStatsConfig
{
    public EquipmentPieceStats Head { get; set; } = new() { HP = 60, Power = 10, Defense = 15 };
    public EquipmentPieceStats Shoulders { get; set; } = new() { HP = 45, Power = 5, Defense = 20 };
    public EquipmentPieceStats Chest { get; set; } = new() { HP = 100, Power = 15, Defense = 35 };
    public EquipmentPieceStats Gloves { get; set; } = new() { Power = 25, HP = 10, Defense = 3 };
    public EquipmentPieceStats Legs { get; set; } = new() { HP = 60, Power = 10, Defense = 15 };
    public EquipmentPieceStats Boots { get; set; } = new() { HP = 30, Power = 5, Defense = 10 };
    public EquipmentPieceStats Instrument { get; set; } = new() { HP = 8, Power = 12, Defense = 5 };
}

/// <summary>
/// Fidelis values for discarding items from inventory.
/// </summary>
public class DiscardValuesConfig
{
    /// <summary>Fidelis gained from discarding an equipment piece.</summary>
    public decimal Equipment { get; set; } = 20m;

    /// <summary>Fidelis gained from discarding an instrument part.</summary>
    public decimal InstrumentPart { get; set; } = 25m;

    /// <summary>Base Fidelis gained from discarding a forged weapon.</summary>
    public decimal Weapon { get; set; } = 50m;
}

/// <summary>
/// Forging configuration for weapon crafting.
/// Uses linear costs: baseCost + currentLevel * costPerLevel.
/// </summary>
public class ForgingConfig
{
    /// <summary>Cast time in seconds for forging a weapon.</summary>
    public int CastTimeSeconds { get; set; } = 5;

    /// <summary>Base Fidelis cost to upgrade a weapon.</summary>
    public decimal WeaponUpgradeBaseCost { get; set; } = 50m;

    /// <summary>Cost increment per weapon level: cost = baseCost + level * costPerLevel.</summary>
    public decimal WeaponUpgradeCostPerLevel { get; set; } = 50m;

    /// <summary>Maximum weapon upgrade level.</summary>
    public int MaxWeaponLevel { get; set; } = 9999;

    /// <summary>Stat increase percentage per weapon level (0.05 = +5% per level).</summary>
    public double WeaponUpgradeStatBonus { get; set; } = 0.05;

    /// <summary>Base Fidelis cost to upgrade equipment enhancement.</summary>
    public decimal EquipmentUpgradeBaseCost { get; set; } = 40m;

    /// <summary>Cost increment per equipment upgrade level.</summary>
    public decimal EquipmentUpgradeCostPerLevel { get; set; } = 40m;

    /// <summary>
    /// Stat multiplier for two-handed weapons.
    /// Default 2.0 = same total power as equipping two 1H weapons.
    /// </summary>
    public double TwoHandedMultiplier { get; set; } = 2.0;

    /// <summary>Minimum quality multiplier for instrument-based weapons.</summary>
    public double InstrumentQualityMin { get; set; } = 0.85;

    /// <summary>Maximum quality multiplier for instrument-based weapons.</summary>
    public double InstrumentQualityMax { get; set; } = 1.15;

    /// <summary>Minimum drink EnergyCost required to roll critical chance.</summary>
    public int CritMinDrinkCost { get; set; } = 7;

    /// <summary>Probability that an eligible weapon rolls a crit bonus.</summary>
    public double CritRollChance { get; set; } = 0.30;

    /// <summary>Minimum critical chance value when rolled.</summary>
    public double CritMin { get; set; } = 0.01;

    /// <summary>Maximum critical chance value when rolled.</summary>
    public double CritMax { get; set; } = 0.10;

    /// <summary>Minimum drink EnergyCost required to roll a speed bonus.</summary>
    public int SpeedMinDrinkCost { get; set; } = 7;

    /// <summary>Probability that an eligible weapon rolls a speed bonus.</summary>
    public double SpeedRollChance { get; set; } = 0.25;

    /// <summary>Minimum speed bonus value when rolled.</summary>
    public int SpeedMin { get; set; } = 1;

    /// <summary>Maximum speed bonus value when rolled.</summary>
    public int SpeedMax { get; set; } = 5;

    /// <summary>Number of upgrade levels before advancing to the next drink tier.</summary>
    public int UpgradeLevelsPerDrinkTier { get; set; } = 50;

    /// <summary>
    /// Stat bonus multiplier per drink tier above Cerveja.
    /// Formula: drinkTierMult = 1.0 + (energyCost - 1) × DrinkStatBonusPerTier.
    /// With 0.25: Cerveja=1.0×, Vinho=1.25×, Licor=1.50×, … Aguardente=3.25×.
    /// </summary>
    public double DrinkStatBonusPerTier { get; set; } = 0.25;
}

/// <summary>
/// Biome configuration for infinite stage progression
/// </summary>
public class BiomeConfig
{
    /// <summary>Biome name (e.g., "Forest", "Desert")</summary>
    public string Name { get; set; } = "Forest";

    /// <summary>Minimum stage number for this biome (inclusive)</summary>
    public int StageMin { get; set; } = 1;

    /// <summary>Maximum stage number for this biome (inclusive)</summary>
    public int StageMax { get; set; } = 100;

    /// <summary>Path to enemy sprite folder (relative to wwwroot)</summary>
    public string EnemySpritePath { get; set; } = "sprites/games/my-tuno/enemies/forest";

    /// <summary>Prefix for boss sprite filenames</summary>
    public string BossSpritePrefix { get; set; } = "boss_";

    /// <summary>Fidelis/reward multiplier for this biome.</summary>
    public double RewardMultiplier { get; set; } = 1.0;
}

/// <summary>
/// Encounter rules configuration for stage progression
/// </summary>
public class EncounterRulesConfig
{
    /// <summary>Boss appears every N stages (default 100)</summary>
    public int BossEveryNStages { get; set; } = 100;

    /// <summary>MiniBoss appears every N stages within each boss cycle (default 10)</summary>
    public int MinibossEveryNStages { get; set; } = 10;

    /// <summary>Enemy count rules based on stage offset within each miniboss cycle (1-9)</summary>
    public List<EnemyCountRule> EnemyCountByStageOffset { get; set; } = new();
}

/// <summary>
/// Enemy count rule for a range of stage offsets
/// </summary>
public class EnemyCountRule
{
    public int From { get; set; }
    public int To { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Combat engine configuration
/// </summary>
public class CombatConfig
{
    /// <summary>
    /// Defense mitigation constant K. Formula: multiplier = K / (K + defense).
    /// At K=500, defense=500 gives 50% damage reduction.
    /// </summary>
    public double DefenseK { get; set; } = 500;

    /// <summary>Minimum damage floor after defense mitigation.</summary>
    public int MinDamage { get; set; } = 1;

    /// <summary>Maximum critical chance cap (0.40 = 40%).</summary>
    public double CriticalChanceCap { get; set; } = 0.40;

    /// <summary>Critical hit damage multiplier.</summary>
    public double CritMultiplier { get; set; } = 2.0;

    /// <summary>Stat multiplier when a shot buff is active.</summary>
    public double ShotBuffMultiplier { get; set; } = 1.20;
}

/// <summary>
/// Configuration for consumable item effects during combat.
/// </summary>
public class ConsumablesConfig
{
    public double FinoHealPercent { get; set; } = 0.25;
    public double FinoCooldownSeconds { get; set; } = 150;
    public double CanecaHealPercent { get; set; } = 0.50;
    public double CanecaCooldownSeconds { get; set; } = 300;
    public int ShotBuffRuns { get; set; } = 5;
    public double ShotBuffMultiplier { get; set; } = 1.05;
    public int CigarroBuffRuns { get; set; } = 5;
    public double CigarroDodgeChance { get; set; } = 0.10;
    public int CanhaoBuffMinutes { get; set; } = 6;
    public int PenaltyBuffMinutes { get; set; } = 2;
    public double PenaltyLifestealPercent { get; set; } = 0.005;
}

/// <summary>
/// Configuration for consumable upgrade ranks (improvements to consumable effects).
/// Each upgrade type has fixed cost tiers (doubling or linear) and max ranks.
/// </summary>
public class ConsumableUpgradesConfig
{
    /// <summary>Cigarro dodge upgrade: 5 ranks, doubling cost from 5M.</summary>
    public ConsumableUpgradeTier CigarroDodge { get; set; } = new()
    {
        MaxUpgrades = 5,
        BaseCost = 5_000_000m,
        CostFormula = CostFormulaType.Doubling
    };

    /// <summary>Shot buff upgrade: 5 ranks, doubling cost from 5M.</summary>
    public ConsumableUpgradeTier ShotBuff { get; set; } = new()
    {
        MaxUpgrades = 5,
        BaseCost = 5_000_000m,
        CostFormula = CostFormulaType.Doubling
    };

    /// <summary>Canhão timer upgrade: 3 ranks, doubling cost from 25M.</summary>
    public ConsumableUpgradeTier CanhaoTimer { get; set; } = new()
    {
        MaxUpgrades = 3,
        BaseCost = 25_000_000m,
        CostFormula = CostFormulaType.Doubling
    };

    /// <summary>Penalty timer+lifesteal upgrade: 3 ranks, linear cost from 50M (+50M per rank).</summary>
    public ConsumableUpgradeTier PenaltyTimer { get; set; } = new()
    {
        MaxUpgrades = 3,
        BaseCost = 50_000_000m,
        CostPerLevel = 50_000_000m,
        CostFormula = CostFormulaType.Linear
    };
}

/// <summary>
/// Configuration for a single consumable upgrade tier.
/// </summary>
public class ConsumableUpgradeTier
{
    public int MaxUpgrades { get; set; }
    public decimal BaseCost { get; set; }
    public decimal CostPerLevel { get; set; }
    public CostFormulaType CostFormula { get; set; } = CostFormulaType.Doubling;
}

/// <summary>
/// Determines how upgrade cost scales per rank.
/// </summary>
public enum CostFormulaType
{
    /// <summary>Cost doubles each rank: BaseCost × 2^n</summary>
    Doubling,
    /// <summary>Cost increases linearly: BaseCost + n × CostPerLevel</summary>
    Linear
}

/// <summary>
/// Configuration for the Destilaria (resource gathering) system
/// </summary>
public class GatheringConfig
{
    public int RegenIntervalSeconds { get; set; } = 60;
    public int CastTimeSeconds { get; set; } = 3;
    public List<GatheringResourceConfig> Resources { get; set; } = new();
}

/// <summary>
/// Configuration for a single gathering resource
/// </summary>
public class GatheringResourceConfig
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EnergyCost { get; set; } = 1;
    public int ForgeCost { get; set; } = 1;
    public string Icon { get; set; } = "bi-box";
    public string SpritePath { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int UnlockStage { get; set; } = 1;
}

/// <summary>
/// Configuration for daily login rewards
/// </summary>
public class DailyRewardConfig
{
    /// <summary>Minimum (floor) Fidelis amount for the daily reward.</summary>
    public decimal BaseFidelis { get; set; } = 100m;

    /// <summary>Multiplier for the power-curve formula: reward = Multiplier × level^Exponent.</summary>
    public double Multiplier { get; set; } = 3.6;

    /// <summary>Exponent for the power-curve formula: reward = Multiplier × level^Exponent.</summary>
    public double Exponent { get; set; } = 2.44;

    /// <summary>Legacy linear per-level bonus (kept for backward compatibility, default 0).</summary>
    public decimal PerLevelFidelis { get; set; } = 0m;

    /// <summary>Percentage of current Fidelis balance added as bonus. Set to 0 in v5.</summary>
    public decimal BalancePercent { get; set; } = 0.0m;
}

/// <summary>
/// Configuration for Boss Mode — endless boss-only mode.
/// </summary>
public class BossModeConfig
{
    /// <summary>
    /// Stage offset that maps boss stage 1 to this equivalent stage mode difficulty.
    /// </summary>
    public int StageOffset { get; set; } = 1000;

    /// <summary>
    /// How many equivalent stages each boss stage is worth.
    /// </summary>
    public int BossStageScaling { get; set; } = 10;

    /// <summary>XP base per boss level.</summary>
    public double XpPerBossLevel { get; set; } = 120;

    /// <summary>Power applied to boss level for XP scaling.</summary>
    public double BossLevelXPPower { get; set; } = 0.6;

    /// <summary>XP penalty rate per level above boss.</summary>
    public double XpLevelPenaltyRate { get; set; } = 0.01;

    /// <summary>Minimum XP multiplier floor.</summary>
    public double MinXPLevelMultiplier { get; set; } = 0.1;

    /// <summary>Reward curve level bonus settings.</summary>
    public BossModeRewardCurve RewardCurve { get; set; } = new();

    /// <summary>Base boss stats.</summary>
    public EnemyTypeStat BaseBossStats { get; set; } = new() { Hp = 150, Power = 25, Speed = 6, Defense = 10, CriticalChance = 0.15 };

    /// <summary>Additional multiplier applied on top of the tier stats for boss fights.</summary>
    public double BossStatMultiplier { get; set; } = 2.0;

    /// <summary>Boss stage beyond which quadratic growth kicks in (extra stat scaling).</summary>
    public int DeepBossThreshold { get; set; } = 100;

    /// <summary>Per-stage quadratic growth rate beyond <see cref="DeepBossThreshold"/>.</summary>
    public double DeepBossGrowthRate { get; set; } = 0.003;

    /// <summary>Drop rates for boss mode rewards.</summary>
    public BossModeDropRates DropRates { get; set; } = new();

    /// <summary>Fidelis reward per boss defeated.</summary>
    public BossModeFidelisRewards FidelisRewards { get; set; } = new();

    /// <summary>FITAB drop chance in stage mode.</summary>
    public double FitabDropChanceStage { get; set; } = 0.002;

    /// <summary>FITAB drop chance in arena/battle mode.</summary>
    public double FitabDropChanceBattle { get; set; } = 0.001;

    /// <summary>Path to boss enemy sprites.</summary>
    public string EnemySpritePath { get; set; } = "sprites/games/my-tuno/enemies/jeans";

    /// <summary>Path to the boss mode background image.</summary>
    public string BackgroundPath { get; set; } = "/sprites/games/my-tuno/backgrounds/jeans.png";

    /// <summary>Piggies (Leitão) cost configuration for mid-game upgrades.</summary>
    public PiggiesCostConfig Piggies { get; set; } = new();
}

/// <summary>
/// Boss Mode reward curve using simple linear level bonus.
/// </summary>
public class BossModeRewardCurve
{
    /// <summary>Per-level Fidelis multiplier bonus.</summary>
    public double LevelBonusPerLevel { get; set; } = 0.01;

    /// <summary>Maximum level bonus cap.</summary>
    public double LevelBonusCap { get; set; } = 2.0;
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
/// Drop rates for Boss Mode.
/// </summary>
public class BossModeDropRates
{
    public double FinoDropChance { get; set; } = 0.003;
    public double ShotDropChance { get; set; } = 0.0018;
    public double CigarroDropChance { get; set; } = 0.0018;
    public double CanecaDropChance { get; set; } = 0.0008;
    public double CanhaoDropChance { get; set; } = 0.0006;
    public double PenaltyDropChance { get; set; } = 0.0004;
    public double InstrumentPartDropChance { get; set; } = 0.0006;
    public double FitabDropChance { get; set; } = 0.005;

    /// <summary>Leitão drop chance per boss kill (Boss Mode exclusive currency). Drops exactly 1 on success.</summary>
    public double LeitaoDropChance { get; set; } = 0.15;
}

/// <summary>
/// Fidelis reward config for Boss Mode.
/// </summary>
public class BossModeFidelisRewards
{
    public decimal BossWin { get; set; } = 430m;
}

/// <summary>
/// Piggies (Leitão) cost configuration — determines how many Leitão are required
/// for various upgrade types once the player reaches mid-game levels.
/// Cost = BaseCost + floor(currentLevel / CostEveryNLevels) for levels >= StartLevel.
/// </summary>
public class PiggiesCostConfig
{
    /// <summary>Stat upgrade: level at which Leitão cost kicks in (stage ~10k).</summary>
    public int StatUpgradeStartLevel { get; set; } = 601;
    /// <summary>Speed upgrade: level at which Leitão cost kicks in (mid-cap).</summary>
    public int SpeedUpgradeStartLevel { get; set; } = 20;
    /// <summary>Crit upgrade: level at which Leitão cost kicks in (mid-cap).</summary>
    public int CritUpgradeStartLevel { get; set; } = 50;
    /// <summary>Base Leitão cost for stat upgrades.</summary>
    public int StatUpgradeBaseCost { get; set; } = 1;
    /// <summary>Every N upgrade levels, add +1 Leitão cost.</summary>
    public int StatUpgradeCostEveryNLevels { get; set; } = 100;

    /// <summary>Improvement upgrade: level at which Leitão cost kicks in.</summary>
    public int ImprovementStartLevel { get; set; } = 20;
    public int ImprovementBaseCost { get; set; } = 1;
    public int ImprovementCostEveryNLevels { get; set; } = 5;

    /// <summary>Power upgrade: level at which Leitão cost kicks in.</summary>
    public int PowerStartLevel { get; set; } = 20;
    public int PowerBaseCost { get; set; } = 1;
    public int PowerCostEveryNLevels { get; set; } = 5;

    /// <summary>Weapon upgrade: level at which Leitão cost kicks in.</summary>
    public int WeaponUpgradeStartLevel { get; set; } = 601;
    public int WeaponUpgradeBaseCost { get; set; } = 1;
    public int WeaponUpgradeCostEveryNLevels { get; set; } = 100;

    /// <summary>Equipment slot upgrade: level at which Leitão cost kicks in.</summary>
    public int EquipmentUpgradeStartLevel { get; set; } = 601;
    public int EquipmentUpgradeBaseCost { get; set; } = 1;
    public int EquipmentUpgradeCostEveryNLevels { get; set; } = 100;

    /// <summary>
    /// Calculates the Leitão cost for a given upgrade level.
    /// Returns 0 if below the start level.
    /// </summary>
    public static int CalculateCost(int currentLevel, int startLevel, int baseCost, int costEveryNLevels)
    {
        if (currentLevel < startLevel) return 0;
        var levelsAbove = currentLevel - startLevel;
        return baseCost + (costEveryNLevels > 0 ? levelsAbove / costEveryNLevels : 0);
    }

    /// <summary>
    /// Gets the appropriate start level for a given stat type.
    /// Speed and Crit have their own thresholds; HP/Power/Defense share StatUpgradeStartLevel.
    /// </summary>
    public int GetStartLevelForStat(Core.Enums.StatType statType) => statType switch
    {
        Core.Enums.StatType.Speed => SpeedUpgradeStartLevel,
        Core.Enums.StatType.CriticalChance => CritUpgradeStartLevel,
        _ => StatUpgradeStartLevel
    };
}
