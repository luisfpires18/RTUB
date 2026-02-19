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

    public static int XpPerLevelBase { get; private set; } = 100;

    /// <summary>
    /// Exponent for XP growth: XP needed = XpPerLevelBase × Level^XpGrowthExponent.
    /// With 1.5, level 100 needs ~100,000 XP.
    /// </summary>
    public static double XpGrowthExponent { get; private set; } = 1.5;

    // ── Upgrade flat bonuses: TotalStat = (base + flatBonus × n) × levelFactor ──

    /// <summary>Flat HP added per upgrade. TotalHP = (BaseHP + HpFlatBonus × n) × LevelFactor.</summary>
    public static double HpFlatBonus { get; private set; } = 100;

    /// <summary>Flat Power added per upgrade.</summary>
    public static double PowerFlatBonus { get; private set; } = 15;

    /// <summary>Flat speed bonus per upgrade (additive). ActionTime = 5 - n × flatBonus × 0.065.</summary>
    public static double SpeedFlatBonus { get; private set; } = 1.5;

    /// <summary>Flat critical chance added per upgrade (additive, capped at MaxCriticalChance).</summary>
    public static double CriticalChancePerUpgrade { get; private set; } = 0.005;

    /// <summary>Flat Defense added per upgrade.</summary>
    public static double DefenseFlatBonus { get; private set; } = 12;

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
    public static double CigarroDodgeChance { get; private set; } = 0.10;

    /// <summary>Number of runs the Canhão AOE buff persists.</summary>
    public static int CanhaoBuffRuns { get; private set; } = 5;

    /// <summary>Number of runs the Penalty lifesteal buff persists.</summary>
    public static int PenaltyBuffRuns { get; private set; } = 5;

    /// <summary>HP lifesteal per hit from Penalty buff (0.005 = 0.5%).</summary>
    public static double PenaltyLifestealPercent { get; private set; } = 0.005;

    // ── Improvements (game-wide improvements) ──

    /// <summary>Base maximum energy capacity.</summary>
    public static int BaseMaxEnergy { get; private set; } = 10;

    /// <summary>Energy capacity increase per upgrade level.</summary>
    public static double EnergyAmountPerUpgrade { get; private set; } = 2.0;

    /// <summary>Base energy regeneration interval in seconds.</summary>
    public static double BaseRegenInterval { get; private set; } = 60.0;

    /// <summary>Regen interval reduction per upgrade level (seconds).</summary>
    public static double RegenReductionPerUpgrade { get; private set; } = 2.0;

    // ── Powers (combat power enhancements) ──

    /// <summary>Heavy attack damage bonus per upgrade (additive to base 2.0x multiplier).</summary>
    public static double HeavyAttackBonusPerUpgrade { get; private set; } = 0.05;

    /// <summary>Special attack damage bonus per upgrade (additive to base multiplier).</summary>
    public static double SpecialAttackBonusPerUpgrade { get; private set; } = 0.05;

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
        double heavyAttackBonusPerUpgrade = 0.05,
        double specialAttackBonusPerUpgrade = 0.05,
        double finoHealPercent = 0.25,
        double finoCooldownSeconds = 150,
        double canecaHealPercent = 0.50,
        double canecaCooldownSeconds = 300,
        int shotBuffRuns = 5,
        double shotBuffMultiplierConsumable = 1.05,
        int cigarroBuffRuns = 5,
        double cigarroDodgeChance = 0.10,
        int canhaoBuffRuns = 5,
        int penaltyBuffRuns = 5,
        double penaltyLifestealPercent = 0.005)
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
        HeavyAttackBonusPerUpgrade = heavyAttackBonusPerUpgrade;
        SpecialAttackBonusPerUpgrade = specialAttackBonusPerUpgrade;
        FinoHealPercent = finoHealPercent;
        FinoCooldownSeconds = finoCooldownSeconds;
        CanecaHealPercent = canecaHealPercent;
        CanecaCooldownSeconds = canecaCooldownSeconds;
        ShotBuffRuns = shotBuffRuns;
        ShotBuffMultiplier = shotBuffMultiplierConsumable;
        CigarroBuffRuns = cigarroBuffRuns;
        CigarroDodgeChance = cigarroDodgeChance;
        CanhaoBuffRuns = canhaoBuffRuns;
        PenaltyBuffRuns = penaltyBuffRuns;
        PenaltyLifestealPercent = penaltyLifestealPercent;
    }
}
