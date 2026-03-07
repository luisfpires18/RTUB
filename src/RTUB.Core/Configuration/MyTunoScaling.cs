namespace RTUB.Core.Configuration;

/// <summary>
/// Static singleton holding all runtime scaling constants for My Tuno (v5.0.0).
/// Uses logarithmic stat curves, linear costs, and tiered enemy tables.
/// </summary>
public static class MyTunoScaling
{
    // ── Base stats ──
    public static int BaseLevel { get; private set; } = 1;
    public static int BaseXp { get; private set; } = 0;
    public static int BaseHp { get; private set; } = 200;
    public static int BasePower { get; private set; } = 25;
    public static int BaseSpeed { get; private set; } = 10;
    public static int BaseDefense { get; private set; } = 20;
    public static double BaseCriticalChance { get; private set; } = 0.00;

    // ── Level scaling (linear) ──
    public static int MaxLevel { get; private set; } = 100;

    /// <summary>
    /// Linear bonus per level: LevelFactor = 1 + BonusPerLevel × (Level - 1).
    /// At level 100: factor = 1 + 0.008 × 99 = 1.792.
    /// </summary>
    public static double BonusPerLevel { get; private set; } = 0.008;

    /// <summary>
    /// Character level at which the enhanced post-piggies bonus per level kicks in.
    /// Before this level the base <see cref="BonusPerLevel"/> is used; from this level
    /// onward <see cref="PostPiggiesBonusPerLevel"/> applies instead.
    /// </summary>
    public static int PostPiggiesStartLevel { get; private set; } = 1000;

    /// <summary>
    /// Enhanced bonus per level applied from <see cref="PostPiggiesStartLevel"/> onward.
    /// Default 0.016 (double the base 0.008) to make stat upgrades more rewarding once piggies are required.
    /// </summary>
    public static double PostPiggiesBonusPerLevel { get; private set; } = 0.016;

    public static int XpPerLevelBase { get; private set; } = 100;

    /// <summary>
    /// Exponent for XP growth: XP needed = XpPerLevelBase × Level^XpGrowthExponent.
    /// With 1.5, level 100 needs ~100,000 XP.
    /// </summary>
    public static double XpGrowthExponent { get; private set; } = 1.5;

    // ── Upgrade flat bonuses: TotalStat = (base + flatBonus × n) × levelFactor ──

    /// <summary>Flat HP added per upgrade. TotalHP = (BaseHP + HpFlatBonus × n) × LevelFactor.</summary>
    public static double HpFlatBonus { get; private set; } = 200;

    /// <summary>Flat Power added per upgrade.</summary>
    public static double PowerFlatBonus { get; private set; } = 30;

    /// <summary>Flat speed bonus per upgrade (additive). ActionTime = 5 - n × flatBonus × 0.065.</summary>
    public static double SpeedFlatBonus { get; private set; } = 1.5;

    /// <summary>Flat critical chance added per upgrade (additive, capped at MaxCriticalChance).</summary>
    public static double CriticalChancePerUpgrade { get; private set; } = 0.005;

    /// <summary>Flat Defense added per upgrade.</summary>
    public static double DefenseFlatBonus { get; private set; } = 24;

    /// <summary>
    /// Compound growth rate per upgrade level for HP/Power/Defense.
    /// Each successive upgrade gives (1 + growthRate × upgradeIndex) × flatBonus.
    /// Total from N upgrades = flatBonus × N × (1 + growthRate × (N-1)/2).
    /// At 0.001, upgrade #1000 gives twice as much as upgrade #1.
    /// </summary>
    public static double UpgradeGrowthRate { get; private set; } = 0.001;

    /// <summary>
    /// Computes the cumulative stat bonus from N upgrades with compound growth.
    /// Sum of flatBonus × (1 + growthRate × i) for i = 0..N-1
    /// = flatBonus × N × (1 + growthRate × (N - 1) / 2)
    /// </summary>
    public static double CumulativeUpgradeBonus(double flatBonus, int upgrades)
    {
        if (upgrades <= 0) return 0;
        return flatBonus * upgrades * (1.0 + UpgradeGrowthRate * (upgrades - 1) / 2.0);
    }

    /// <summary>
    /// Computes the marginal stat bonus for the Nth upgrade (0-indexed).
    /// = flatBonus × (1 + growthRate × N)
    /// </summary>
    public static double MarginalUpgradeBonus(double flatBonus, int upgradeIndex)
    {
        return flatBonus * (1.0 + UpgradeGrowthRate * Math.Max(0, upgradeIndex));
    }

    // ── Combat ──

    /// <summary>
    /// Defense constant K for damage mitigation: mult = K / (K + defense).
    /// At K=500, defense=500 gives 50% reduction.
    /// </summary>
    public static double DefenseK { get; private set; } = 500;

    /// <summary>Minimum damage floor after defense mitigation.</summary>
    public static int MinDamage { get; private set; } = 1;

    /// <summary>Maximum critical chance cap (0.50 = 50%).</summary>
    public static double MaxCriticalChance { get; private set; } = 0.50;

    /// <summary>Critical hit damage multiplier.</summary>
    public static double CritMultiplier { get; private set; } = 2.0;

    /// <summary>Stat multiplier when a shot buff is active (1.20 = 20% boost).</summary>
    public static double ShotBuffMultiplier { get; private set; } = 1.05;

    // ── Consumables ──

    /// <summary>Fino heal as fraction of max HP (0.25 = 25%).</summary>
    public static double FinoHealPercent { get; private set; } = 0.25;

    /// <summary>Fino cooldown in seconds after use.</summary>
    public static double FinoCooldownSeconds { get; private set; } = 150;

    /// <summary>Caneca heal as fraction of max HP (0.50 = 50%).</summary>
    public static double CanecaHealPercent { get; private set; } = 0.50;

    /// <summary>Caneca cooldown in seconds after use.</summary>
    public static double CanecaCooldownSeconds { get; private set; } = 300;

    /// <summary>Number of runs the Shot buff persists.</summary>
    public static int ShotBuffRuns { get; private set; } = 5;

    /// <summary>Number of runs the Cigarro dodge buff persists.</summary>
    public static int CigarroBuffRuns { get; private set; } = 5;

    /// <summary>Dodge chance granted by Cigarro buff (0.10 = 10%).</summary>
    public static double CigarroDodgeChance { get; private set; } = 0.05;

    /// <summary>Duration in minutes for the Canhão AOE buff.</summary>
    public static int CanhaoBuffMinutes { get; private set; } = 6;

    /// <summary>Duration in minutes for the Penalty lifesteal buff.</summary>
    public static int PenaltyBuffMinutes { get; private set; } = 2;

    /// <summary>HP lifesteal per hit from Penalty buff (0.005 = 0.5%).</summary>
    public static double PenaltyLifestealPercent { get; private set; } = 0.005;

    // ── Consumable Upgrades (rank-based improvements to consumable effects) ──

    /// <summary>Cigarro dodge chance increase per upgrade (0.04 = +4% per rank).</summary>
    public static double CigarroDodgePerUpgrade { get; private set; } = 0.04;

    /// <summary>Maximum Cigarro dodge chance after upgrades (0.25 = 25%).</summary>
    public static double MaxCigarroDodge { get; private set; } = 0.25;

    /// <summary>Shot buff multiplier increase per upgrade (0.05 = +5% per rank).</summary>
    public static double ShotBuffPerUpgrade { get; private set; } = 0.05;

    /// <summary>Maximum Shot buff multiplier after upgrades (1.30 = +30%).</summary>
    public static double MaxShotBuffMultiplier { get; private set; } = 1.30;

    /// <summary>Canhão duration increase per upgrade in minutes.</summary>
    public static int CanhaoMinutesPerUpgrade { get; private set; } = 1;

    /// <summary>Maximum Canhão duration in minutes after upgrades.</summary>
    public static int MaxCanhaoMinutes { get; private set; } = 5;

    /// <summary>Penalty duration increase per upgrade in minutes.</summary>
    public static int PenaltyMinutesPerUpgrade { get; private set; } = 1;

    /// <summary>Maximum Penalty duration in minutes after upgrades.</summary>
    public static int MaxPenaltyMinutes { get; private set; } = 5;

    /// <summary>Penalty lifesteal percent increase per upgrade.</summary>
    public static double PenaltyLifestealPerUpgrade { get; private set; } = 0.003333;

    /// <summary>Maximum Penalty lifesteal percent after upgrades (0.015 = 1.5%).</summary>
    public static double MaxPenaltyLifesteal { get; private set; } = 0.015;

    // ── Improvements (game-wide improvements) ──

    /// <summary>Base maximum energy capacity.</summary>
    public static int BaseMaxEnergy { get; private set; } = 10;

    /// <summary>Energy capacity increase per upgrade level.</summary>
    public static double EnergyAmountPerUpgrade { get; private set; } = 2.0;

    /// <summary>Base energy regeneration interval in seconds.</summary>
    public static double BaseRegenInterval { get; private set; } = 60.0;

    /// <summary>Regen interval reduction per upgrade level (seconds).</summary>
    public static double RegenReductionPerUpgrade { get; private set; } = 2.0;

    /// <summary>Base gathering cast time in seconds.</summary>
    public static double BaseCastTime { get; private set; } = 3.0;

    /// <summary>Cast time reduction per upgrade level (seconds).</summary>
    public static double CastTimeReductionPerUpgrade { get; private set; } = 0.1;

    /// <summary>Minimum cast time floor (seconds).</summary>
    public static double MinCastTime { get; private set; } = 0.1;

    /// <summary>Double gathering chance per upgrade level (e.g. 0.02 = 2% per level).</summary>
    public static double DoubleGatheringChancePerUpgrade { get; private set; } = 0.02;

    /// <summary>Maximum double gathering chance cap (0.50 = 50%).</summary>
    public static double MaxDoubleGatheringChance { get; private set; } = 0.50;

    // ── Rare Set (ultra-rare equipment upgrades) ──

    /// <summary>Crit chance bonus per applied rare set piece (0.05 = 5%).</summary>
    public static double RareSetCritPerPiece { get; private set; } = 0.05;

    /// <summary>Action time reduction per applied rare set piece in seconds.</summary>
    public static double RareSetSpeedPerPiece { get; private set; } = 0.05;

    /// <summary>Minimum rare set pieces needed to activate set bonus (replaces individual bonuses).</summary>
    public static int RareSetBonusMinPieces { get; private set; } = 6;

    /// <summary>Set bonus crit chance (replaces per-piece crit). 0.50 = 50%.</summary>
    public static double RareSetBonusCrit { get; private set; } = 0.50;

    /// <summary>Set bonus action time reduction in seconds (replaces per-piece speed).</summary>
    public static double RareSetBonusSpeedReduction { get; private set; } = 0.5;

    /// <summary>
    /// Configures all scaling values at startup from <c>scaling.config.json</c>.
    /// </summary>
    public static void Configure(
        int baseLevel,
        int baseXp,
        int baseHp,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        int maxLevel,
        double bonusPerLevel,
        int postPiggiesStartLevel,
        double postPiggiesBonusPerLevel,
        int xpPerLevelBase,
        double xpGrowthExponent,
        double hpFlatBonus,
        double powerFlatBonus,
        double speedFlatBonus,
        double criticalChancePerUpgrade,
        double defenseFlatBonus,
        double defenseK,
        int minDamage,
        double maxCriticalChance,
        double critMultiplier,
        double shotBuffMultiplier,
        int baseMaxEnergy = 10,
        double energyAmountPerUpgrade = 2.0,
        double baseRegenInterval = 60.0,
        double regenReductionPerUpgrade = 2.0,
        double baseCastTime = 3.0,
        double castTimeReductionPerUpgrade = 0.1,
        double minCastTime = 0.1,
        double finoHealPercent = 0.25,
        double finoCooldownSeconds = 150,
        double canecaHealPercent = 0.50,
        double canecaCooldownSeconds = 300,
        int shotBuffRuns = 5,
        double shotBuffMultiplierConsumable = 1.05,
        int cigarroBuffRuns = 5,
        double cigarroDodgeChance = 0.05,
        int canhaoBuffMinutes = 6,
        int penaltyBuffMinutes = 3,
        double penaltyLifestealPercent = 0.005,
        double rareSetCritPerPiece = 0.05,
        double rareSetSpeedPerPiece = 0.05,
        int rareSetBonusMinPieces = 6,
        double rareSetBonusCrit = 0.50,
        double rareSetBonusSpeedReduction = 0.5,
        double doubleGatheringChancePerUpgrade = 0.02,
        double maxDoubleGatheringChance = 0.50,
        double cigarroDodgePerUpgrade = 0.04,
        double maxCigarroDodge = 0.25,
        double shotBuffPerUpgrade = 0.05,
        double maxShotBuffMultiplier = 1.30,
        int canhaoMinutesPerUpgrade = 1,
        int maxCanhaoMinutes = 5,
        int penaltyMinutesPerUpgrade = 1,
        int maxPenaltyMinutes = 5,
        double penaltyLifestealPerUpgrade = 0.003333,
        double maxPenaltyLifesteal = 0.015,
        double upgradeGrowthRate = 0.001)
    {
        BaseLevel = baseLevel;
        BaseXp = baseXp;
        BaseHp = baseHp;
        BasePower = basePower;
        BaseSpeed = baseSpeed;
        BaseDefense = baseDefense;
        BaseCriticalChance = baseCriticalChance;
        MaxLevel = maxLevel;
        BonusPerLevel = bonusPerLevel;
        PostPiggiesStartLevel = postPiggiesStartLevel;
        PostPiggiesBonusPerLevel = postPiggiesBonusPerLevel;
        XpPerLevelBase = xpPerLevelBase;
        XpGrowthExponent = xpGrowthExponent;
        HpFlatBonus = hpFlatBonus;
        PowerFlatBonus = powerFlatBonus;
        SpeedFlatBonus = speedFlatBonus;
        CriticalChancePerUpgrade = criticalChancePerUpgrade;
        DefenseFlatBonus = defenseFlatBonus;
        DefenseK = defenseK;
        MinDamage = minDamage;
        MaxCriticalChance = maxCriticalChance;
        CritMultiplier = critMultiplier;
        ShotBuffMultiplier = shotBuffMultiplier;
        BaseMaxEnergy = baseMaxEnergy;
        EnergyAmountPerUpgrade = energyAmountPerUpgrade;
        BaseRegenInterval = baseRegenInterval;
        RegenReductionPerUpgrade = regenReductionPerUpgrade;
        BaseCastTime = baseCastTime;
        CastTimeReductionPerUpgrade = castTimeReductionPerUpgrade;
        MinCastTime = minCastTime;
        FinoHealPercent = finoHealPercent;
        FinoCooldownSeconds = finoCooldownSeconds;
        CanecaHealPercent = canecaHealPercent;
        CanecaCooldownSeconds = canecaCooldownSeconds;
        ShotBuffRuns = shotBuffRuns;
        ShotBuffMultiplier = shotBuffMultiplierConsumable;
        CigarroBuffRuns = cigarroBuffRuns;
        CigarroDodgeChance = cigarroDodgeChance;
        CanhaoBuffMinutes = canhaoBuffMinutes;
        PenaltyBuffMinutes = penaltyBuffMinutes;
        PenaltyLifestealPercent = penaltyLifestealPercent;
        RareSetCritPerPiece = rareSetCritPerPiece;
        RareSetSpeedPerPiece = rareSetSpeedPerPiece;
        RareSetBonusMinPieces = rareSetBonusMinPieces;
        RareSetBonusCrit = rareSetBonusCrit;
        RareSetBonusSpeedReduction = rareSetBonusSpeedReduction;
        DoubleGatheringChancePerUpgrade = doubleGatheringChancePerUpgrade;
        MaxDoubleGatheringChance = maxDoubleGatheringChance;
        CigarroDodgePerUpgrade = cigarroDodgePerUpgrade;
        MaxCigarroDodge = maxCigarroDodge;
        ShotBuffPerUpgrade = shotBuffPerUpgrade;
        MaxShotBuffMultiplier = maxShotBuffMultiplier;
        CanhaoMinutesPerUpgrade = canhaoMinutesPerUpgrade;
        MaxCanhaoMinutes = maxCanhaoMinutes;
        PenaltyMinutesPerUpgrade = penaltyMinutesPerUpgrade;
        MaxPenaltyMinutes = maxPenaltyMinutes;
        PenaltyLifestealPerUpgrade = penaltyLifestealPerUpgrade;
        MaxPenaltyLifesteal = maxPenaltyLifesteal;
        UpgradeGrowthRate = upgradeGrowthRate;
    }
}
