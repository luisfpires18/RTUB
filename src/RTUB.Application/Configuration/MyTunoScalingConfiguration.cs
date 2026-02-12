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
    /// Beating higher level = bonus, beating lower level = penalty.
    /// Default 0.02 = 2% per level difference. Zero threshold at 45 levels above.
    /// </summary>
    public double LevelDiffScale { get; set; } = 0.02;

    /// <summary>
    /// Minimum reward multiplier floor. 0.1 = always earn at least 10% of base rewards.
    /// </summary>
    public double MinRewardMultiplier { get; set; } = 0.1;

    /// <summary>
    /// Maximum reward multiplier (cap for beating much stronger opponents).
    /// Default 3.0 = max 300% rewards.
    /// </summary>
    public double MaxRewardMultiplier { get; set; } = 3.0;

    /// <summary>
    /// Level-based scaling exponent for arena rewards.
    /// Rewards scale with attacker level: base × level^LevelScalePower.
    /// 0.5 = sqrt scaling (level 381 → 19.5× base). 0 = no level scaling.
    /// </summary>
    public double LevelScalePower { get; set; } = 0.7;

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
    /// Chance of Fino drop after winning an arena battle (0.0 to 1.0)
    /// </summary>
    public double FinoDropChance { get; set; } = 0.01;

    /// <summary>
    /// Chance of Shot drop after winning an arena battle (0.0 to 1.0)
    /// </summary>
    public double ShotDropChance { get; set; } = 0.006;

    /// <summary>
    /// Chance of Caneca drop after winning an arena battle (0.0 to 1.0)
    /// </summary>
    public double CanecaDropChance { get; set; } = 0.003;

    /// <summary>
    /// Chance of Cigarro drop after winning an arena battle
    /// </summary>
    public double CigarroDropChance { get; set; } = 0.004;

    /// <summary>
    /// Chance of Canhão drop after winning an arena battle
    /// </summary>
    public double CanhaoDropChance { get; set; } = 0.002;

    /// <summary>
    /// Chance of Penalty drop after winning an arena battle
    /// </summary>
    public double PenaltyDropChance { get; set; } = 0.001;
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
    /// <summary>
    /// Maximum player level. After this, XP still accumulates but no more level-ups.
    /// </summary>
    public int MaxLevel { get; set; } = 1000;

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
    public double MultiplierPerUpgrade { get; set; }
    public decimal BaseCost { get; set; }
    public double CostExponent { get; set; } = 1.5;
    /// <summary>
    /// Maximum upgrades allowed. 0 = unlimited (HP/Power/Defense are unlimited).
    /// Only Speed (40) and CriticalChance (100) have hard caps.
    /// </summary>
    public int MaxUpgrades { get; set; } = 0;
}

/// <summary>
/// Configuration for Stage Mode
/// </summary>
public class StageModeConfig
{
    /// <summary>
    /// XP base per enemy level. XP = xpPerEnemyLevel × enemyLevel^enemyLevelXPPower × enemyCount × levelDiffMult.
    /// </summary>
    public double XpPerEnemyLevel { get; set; } = 12;

    /// <summary>
    /// Power applied to enemy level for XP scaling.
    /// 0.5 = sqrt (stage 100 gives 10× stage 1), 1.0 = linear.
    /// </summary>
    public double EnemyLevelXPPower { get; set; } = 0.5;

    /// <summary>
    /// XP penalty rate per level above enemy.
    /// </summary>
    public double XpLevelPenaltyRate { get; set; } = 0.015;

    /// <summary>
    /// Minimum XP multiplier floor when player is much higher level than enemies.
    /// </summary>
    public double MinXPLevelMultiplier { get; set; } = 0.05;

    /// <summary>
    /// XP multiplier for boss stages
    /// </summary>
    public int BossXPMultiplier { get; set; } = 8;

    /// <summary>
    /// Boss stat multiplier applied to all enemy stats when the enemy is a boss.
    /// </summary>
    public double BossMultiplier { get; set; } = 1.2;

    /// <summary>
    /// Unified difficulty curve for ALL enemy stats.
    /// Formula: 1 + scalingRate × (stage - 1) ^ growthExponent.
    /// </summary>
    public DifficultyCurveConfig DifficultyCurve { get; set; } = new();

    /// <summary>
    /// Unified reward curve for Fidelis scaling.
    /// </summary>
    public RewardCurveConfig RewardCurve { get; set; } = new();

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
    /// Per-level scaling factor for equipment stat bonuses.
    /// Formula: equipStat × (1 + level × EquipmentLevelScale).
    /// </summary>
    public double EquipmentLevelScale { get; set; } = 0.05;

    /// <summary>
    /// Per-level scaling factor for equipment discard Fidelis.
    /// Formula: discardValue × (1 + level × DiscardLevelScale).
    /// </summary>
    public double DiscardLevelScale { get; set; } = 0.01;

    /// <summary>
    /// Bonus per equipment enhancement tier.
    /// Enhancement = floor(highestStage / 100). Formula: stat × (1 + enhancement × EquipmentEnhancementBonus).
    /// </summary>
    public double EquipmentEnhancementBonus { get; set; } = 0.5;

    /// <summary>
    /// Per-character-level scaling factor applied to equipped weapon stat bonuses.
    /// Formula: weaponStat × (1 + charLevel × WeaponCharacterLevelScale).
    /// </summary>
    public double WeaponCharacterLevelScale { get; set; } = 0.02;

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
    public double FinoDropChance { get; set; } = 0.01;
    public double ShotDropChance { get; set; } = 0.006;
    public double CigarroDropChance { get; set; } = 0.004;
    public double CanecaDropChance { get; set; } = 0.003;
    public double CanhaoDropChance { get; set; } = 0.002;
    public double PenaltyDropChance { get; set; } = 0.001;
    public double InstrumentPartDropChance { get; set; } = 0.005;
    public double EquipmentDropChance { get; set; } = 0.008;
    public double BossDropMultiplier { get; set; } = 3.0;
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
    public EquipmentPieceStats Head { get; set; } = new() { HP = 60, Power = 10, Defense = 15 };
    public EquipmentPieceStats Shoulders { get; set; } = new() { HP = 45, Power = 5, Defense = 20 };
    public EquipmentPieceStats Chest { get; set; } = new() { HP = 100, Power = 15, Defense = 35 };
    public EquipmentPieceStats Gloves { get; set; } = new() { Power = 25, HP = 10, Defense = 3 };
    public EquipmentPieceStats Legs { get; set; } = new() { HP = 60, Power = 10, Defense = 15 };
    public EquipmentPieceStats Boots { get; set; } = new() { HP = 30, Power = 5, Defense = 10 };
    public EquipmentPieceStats Instrument { get; set; } = new() { Power = 12 };
}

/// <summary>
/// Fidelis values for discarding items from inventory.
/// </summary>
public class DiscardValuesConfig
{
    /// <summary>Fidelis gained from discarding an equipment piece.</summary>
    public decimal Equipment { get; set; } = 200m;

    /// <summary>Fidelis gained from discarding an instrument part.</summary>
    public decimal InstrumentPart { get; set; } = 250m;

    /// <summary>Base Fidelis gained from discarding a forged weapon.</summary>
    public decimal Weapon { get; set; } = 300m;
}

/// <summary>
/// Forging configuration for weapon crafting
/// </summary>
public class ForgingConfig
{
    /// <summary>Cast time in seconds for forging a weapon.</summary>
    public int CastTimeSeconds { get; set; } = 5;

    /// <summary>Base Fidelis cost to upgrade a weapon from level 0 to 1.</summary>
    public decimal WeaponUpgradeBaseCost { get; set; } = 500m;

    /// <summary>Cost multiplier per level: cost = BaseCost * (Multiplier ^ currentLevel).</summary>
    public decimal WeaponUpgradeCostMultiplier { get; set; } = 1.08m;

    /// <summary>Stat increase percentage per weapon level (0.15 = +15% per level).</summary>
    public double WeaponUpgradeStatBonus { get; set; } = 0.15;

    /// <summary>Base Fidelis cost to upgrade equipment enhancement from level 0 to 1.</summary>
    public decimal EquipmentUpgradeBaseCost { get; set; } = 400m;

    /// <summary>Cost multiplier per equipment upgrade level: cost = BaseCost * (Multiplier ^ currentLevel).</summary>
    public decimal EquipmentUpgradeCostMultiplier { get; set; } = 1.12m;

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
    /// Overall difficulty multiplier for this biome.
    /// Range 1.0–1.5 (biome = flavor, not major power change).
    /// Applied on top of the per-stage scaling curve.
    /// </summary>
    public double DifficultyMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Fidelis/reward multiplier for this biome. Range: 1.0–2.5.
    /// Harder/deeper biomes reward more Fidelis.
    /// </summary>
    public double RewardMultiplier { get; set; } = 1.0;
}

/// <summary>
/// Unified difficulty curve configuration.
/// Single polynomial: 1 + scalingRate × (stage - 1) ^ growthExponent.
/// Replaces the old multi-layer system (enemyScaling + scaling + biome integer multipliers).
/// </summary>
public class DifficultyCurveConfig
{
    /// <summary>
    /// Coefficient for the difficulty polynomial. Controls the magnitude of scaling.
    /// Higher = enemies get stronger faster per stage.
    /// </summary>
    public double ScalingRate { get; set; } = 0.12;

    /// <summary>
    /// Exponent for the difficulty polynomial. Controls the shape of the curve.
    /// 1.0 = linear, >1.0 = polynomial (accelerating), <1.0 = sublinear (decelerating).
    /// </summary>
    public double GrowthExponent { get; set; } = 1.15;
}

/// <summary>
/// Unified reward curve configuration.
/// Single polynomial: 1 + scalingRate × (stage - 1) ^ growthExponent.
/// Replaces stageRewardScalingFactor + maxStageRewardMultiplier + fidelisLevelMultiplier stacking.
/// </summary>
public class RewardCurveConfig
{
    /// <summary>
    /// Coefficient for the reward polynomial.
    /// </summary>
    public double ScalingRate { get; set; } = 0.08;

    /// <summary>
    /// Exponent for the reward polynomial.
    /// </summary>
    public double GrowthExponent { get; set; } = 1.10;

    /// <summary>
    /// Per-level Fidelis multiplier bonus. Rewards increase with player level.
    /// Formula: min(1 + (level-1) × LevelBonusPerLevel, LevelBonusCap)
    /// </summary>
    public double LevelBonusPerLevel { get; set; } = 0.04;

    /// <summary>
    /// Maximum level bonus cap.
    /// </summary>
    public double LevelBonusCap { get; set; } = 1.8;
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
/// Combat engine configuration for battle simulation
/// </summary>
public class CombatConfig
{
    /// <summary>
    /// Maximum critical chance cap for enemies (0.5 = 50%)
    /// </summary>
    public double CriticalChanceCap { get; set; } = 0.5;

    /// <summary>
    /// Defense mitigation constant K. Formula: multiplier = K / (K + defense).
    /// Higher K = defense matters less.
    /// </summary>
    public double DefenseK { get; set; } = 50;

    /// <summary>
    /// Minimum damage floor after defense mitigation.
    /// </summary>
    public int MinDamage { get; set; } = 1;

    /// <summary>
    /// Stat multiplier when a shot buff is active (1.20 = 20% boost).
    /// </summary>
    public double ShotBuffMultiplier { get; set; } = 1.20;
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

/// <summary>
/// Configuration for Boss Mode — endless boss-only mode.
/// Boss stage 1 starts at stage mode 500+ difficulty scale.
/// Requires 1 FITAB to enter.
/// </summary>
public class BossModeConfig
{
    /// <summary>
    /// Stage offset that maps boss stage 1 to this equivalent stage mode difficulty.
    /// Boss stage N has difficulty of stage (StageOffset + N - 1).
    /// Default 500 means boss stage 1 ≈ stage mode 500 difficulty.
    /// </summary>
    public int StageOffset { get; set; } = 500;

    /// <summary>
    /// XP base per boss level.
    /// </summary>
    public double XpPerBossLevel { get; set; } = 25;

    /// <summary>
    /// Power applied to boss level for XP scaling.
    /// </summary>
    public double BossLevelXPPower { get; set; } = 0.6;

    /// <summary>
    /// XP penalty rate per level above boss.
    /// </summary>
    public double XpLevelPenaltyRate { get; set; } = 0.01;

    /// <summary>
    /// Minimum XP multiplier floor.
    /// </summary>
    public double MinXPLevelMultiplier { get; set; } = 0.1;

    /// <summary>
    /// Difficulty curve for boss stat scaling.
    /// </summary>
    public DifficultyCurveConfig DifficultyCurve { get; set; } = new();

    /// <summary>
    /// Reward curve for Fidelis scaling.
    /// </summary>
    public RewardCurveConfig RewardCurve { get; set; } = new();

    /// <summary>
    /// Base boss stats (all bosses use these as the starting point).
    /// </summary>
    public EnemyTypeStat BaseBossStats { get; set; } = new() { Hp = 80, Power = 15, Speed = 3, Defense = 4, CriticalChance = 0.10 };

    /// <summary>
    /// Additional multiplier applied on top of the difficulty curve for boss fights.
    /// </summary>
    public double BossStatMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Drop rates for boss mode rewards.
    /// </summary>
    public BossModeDropRates DropRates { get; set; } = new();

    /// <summary>
    /// Fidelis reward per boss defeated.
    /// </summary>
    public BossModeFidelisRewards FidelisRewards { get; set; } = new();

    /// <summary>
    /// FITAB drop chance in stage mode (per enemy killed).
    /// Very low chance — FITAB is a rare drop.
    /// </summary>
    public double FitabDropChanceStage { get; set; } = 0.002;

    /// <summary>
    /// FITAB drop chance in arena/battle mode (per battle won).
    /// </summary>
    public double FitabDropChanceBattle { get; set; } = 0.001;

    /// <summary>
    /// Path to boss enemy sprites (relative to wwwroot).
    /// </summary>
    public string EnemySpritePath { get; set; } = "sprites/games/my-tuno/enemies/jeans";

    /// <summary>
    /// Path to the boss mode background image.
    /// </summary>
    public string BackgroundPath { get; set; } = "/sprites/games/my-tuno/backgrounds/jeans.png";
}

/// <summary>
/// Drop rates for Boss Mode.
/// </summary>
public class BossModeDropRates
{
    public double FinoDropChance { get; set; } = 0.01;
    public double ShotDropChance { get; set; } = 0.006;
    public double CigarroDropChance { get; set; } = 0.004;
    public double CanecaDropChance { get; set; } = 0.003;
    public double CanhaoDropChance { get; set; } = 0.002;
    public double PenaltyDropChance { get; set; } = 0.001;
    public double InstrumentPartDropChance { get; set; } = 0.02;
    public double EquipmentDropChance { get; set; } = 0.015;
    public double FitabDropChance { get; set; } = 0.003;
}

/// <summary>
/// Fidelis reward config for Boss Mode.
/// </summary>
public class BossModeFidelisRewards
{
    public decimal BossWin { get; set; } = 80m;
}
