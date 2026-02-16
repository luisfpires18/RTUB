namespace RTUB.Core.Configuration;

public static class MyTunoScaling
{
    // ── Base stats ──
    public static int BaseLevel { get; private set; } = 1;
    public static int BaseXp { get; private set; } = 0;
    public static int BaseHp { get; private set; } = 100;
    public static int BasePower { get; private set; } = 10;
    public static int BaseSpeed { get; private set; } = 10;
    public static int BaseDefense { get; private set; } = 5;
    public static double BaseCriticalChance { get; private set; } = 0.0;

    // ── Level scaling ──
    public static int MaxLevel { get; private set; } = 1000;
    public static double StatMultiplierPerLevel { get; private set; } = 0.1;
    public static double StatGrowthExponent { get; private set; } = 0.0;
    public static int XpPerLevelBase { get; private set; } = 100;

    /// <summary>
    /// Exponent for exponential XP growth: XP needed = XpPerLevelBase × Level^XpGrowthExponent.
    /// Higher values make late levels require dramatically more XP.
    /// </summary>
    public static double XpGrowthExponent { get; private set; } = 1.0;

    // ── Upgrade multipliers ──
    public static double HpUpgradeMultiplier { get; private set; } = 0.02;
    public static double PowerUpgradeMultiplier { get; private set; } = 0.02;
    public static double SpeedUpgradeMultiplier { get; private set; } = 1;
    public static double CriticalChanceUpgradeMultiplier { get; private set; } = 0.005;
    public static double DefenseUpgradeMultiplier { get; private set; } = 0.02;

    // ── Combat ──
    /// <summary>
    /// Defense constant K for damage mitigation formula: mult = K / (K + defense).
    /// Higher K means defense matters less. When defense = K, damage is reduced by 50%.
    /// </summary>
    public static double DefenseK { get; private set; } = 50;

    /// <summary>
    /// Minimum damage floor after defense mitigation.
    /// </summary>
    public static int MinDamage { get; private set; } = 1;

    /// <summary>
    /// Stat multiplier when a shot buff is active (1.20 = 20% boost).
    /// </summary>
    public static double ShotBuffMultiplier { get; private set; } = 1.20;

    // ── Improvements (game-wide improvements) ──

    /// <summary>Base maximum energy capacity</summary>
    public static int BaseMaxEnergy { get; private set; } = 10;

    /// <summary>Energy capacity increase per upgrade level</summary>
    public static double EnergyAmountPerUpgrade { get; private set; } = 2.0;

    /// <summary>Base energy regeneration interval in seconds</summary>
    public static double BaseRegenInterval { get; private set; } = 60.0;

    /// <summary>Regen interval reduction per upgrade level (seconds)</summary>
    public static double RegenReductionPerUpgrade { get; private set; } = 2.0;

    /// <summary>Shot buff multiplier bonus per upgrade (0.005 = +0.5% per upgrade)</summary>
    public static double ShotBuffBonusPerUpgrade { get; private set; } = 0.005;

    /// <summary>Fidelis earned bonus per upgrade (0.02 = +2% per upgrade)</summary>
    public static double FidelisEarnedBonusPerUpgrade { get; private set; } = 0.02;

    // ── Powers (combat power enhancements) ──

    /// <summary>Heavy attack damage bonus per upgrade (additive to base 2.0x multiplier)</summary>
    public static double HeavyAttackBonusPerUpgrade { get; private set; } = 0.05;

    /// <summary>Special attack damage bonus per upgrade (additive to base multiplier)</summary>
    public static double SpecialAttackBonusPerUpgrade { get; private set; } = 0.05;

    public static void Configure(
        int baseLevel,
        int baseXp,
        int baseHp,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        int maxLevel,
        double statMultiplierPerLevel,
        double statGrowthExponent,
        int xpPerLevelBase,
        double xpGrowthExponent,
        double hpUpgradeMultiplier,
        double powerUpgradeMultiplier,
        double speedUpgradeMultiplier,
        double criticalChanceUpgradeMultiplier,
        double defenseUpgradeMultiplier,
        double defenseK,
        int minDamage,
        double shotBuffMultiplier,
        int baseMaxEnergy = 10,
        double energyAmountPerUpgrade = 2.0,
        double baseRegenInterval = 60.0,
        double regenReductionPerUpgrade = 2.0,
        double shotBuffBonusPerUpgrade = 0.005,
        double fidelisEarnedBonusPerUpgrade = 0.02,
        double heavyAttackBonusPerUpgrade = 0.05,
        double specialAttackBonusPerUpgrade = 0.05)
    {
        BaseLevel = baseLevel;
        BaseXp = baseXp;
        BaseHp = baseHp;
        BasePower = basePower;
        BaseSpeed = baseSpeed;
        BaseDefense = baseDefense;
        BaseCriticalChance = baseCriticalChance;
        MaxLevel = maxLevel;
        StatMultiplierPerLevel = statMultiplierPerLevel;
        StatGrowthExponent = statGrowthExponent;
        XpPerLevelBase = xpPerLevelBase;
        XpGrowthExponent = xpGrowthExponent;
        HpUpgradeMultiplier = hpUpgradeMultiplier;
        PowerUpgradeMultiplier = powerUpgradeMultiplier;
        SpeedUpgradeMultiplier = speedUpgradeMultiplier;
        CriticalChanceUpgradeMultiplier = criticalChanceUpgradeMultiplier;
        DefenseUpgradeMultiplier = defenseUpgradeMultiplier;
        DefenseK = defenseK;
        MinDamage = minDamage;
        ShotBuffMultiplier = shotBuffMultiplier;
        BaseMaxEnergy = baseMaxEnergy;
        EnergyAmountPerUpgrade = energyAmountPerUpgrade;
        BaseRegenInterval = baseRegenInterval;
        RegenReductionPerUpgrade = regenReductionPerUpgrade;
        ShotBuffBonusPerUpgrade = shotBuffBonusPerUpgrade;
        FidelisEarnedBonusPerUpgrade = fidelisEarnedBonusPerUpgrade;
        HeavyAttackBonusPerUpgrade = heavyAttackBonusPerUpgrade;
        SpecialAttackBonusPerUpgrade = specialAttackBonusPerUpgrade;
    }
}
