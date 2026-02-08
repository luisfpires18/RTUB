namespace RTUB.Core.Configuration;

public static class MyTunoScaling
{
    public static int BaseLevel { get; private set; } = 1;
    public static int BaseXp { get; private set; } = 0;
    public static int BaseHp { get; private set; } = 100;
    public static int BasePower { get; private set; } = 10;
    public static int BaseSpeed { get; private set; } = 10;
    public static int BaseDefense { get; private set; } = 5;
    public static double BaseCriticalChance { get; private set; } = 0.01;

    public static double StatMultiplierPerLevel { get; private set; } = 0.1;
    public static double StatGrowthExponent { get; private set; } = 0.0;
    public static int XpPerLevelBase { get; private set; } = 100;

    public static int InitialHpUpgrades { get; private set; } = 0;
    public static int InitialPowerUpgrades { get; private set; } = 0;
    public static int InitialSpeedUpgrades { get; private set; } = 0;
    public static int InitialCriticalUpgrades { get; private set; } = 0;
    public static int InitialDefenseUpgrades { get; private set; } = 0;

    public static double HpUpgradeMultiplier { get; private set; } = 0.02;
    public static double PowerUpgradeMultiplier { get; private set; } = 0.02;
    public static double SpeedUpgradeMultiplier { get; private set; } = 1;
    public static double CriticalChanceUpgradeMultiplier { get; private set; } = 0.005;
    public static double DefenseUpgradeMultiplier { get; private set; } = 0.02;

    /// <summary>
    /// Defense constant K for damage mitigation formula: mult = K / (K + defense)
    /// Higher K means defense is less effective (more damage taken)
    /// </summary>
    public static double DefenseK { get; private set; } = 50;

    /// <summary>
    /// Minimum damage that can be dealt after defense mitigation
    /// </summary>
    public static int MinDamage { get; private set; } = 1;

    /// <summary>
    /// Enemy speed scaling rate relative to StatMultiplierPerLevel.
    /// Controls how fast enemies' speed grows per stage in the polynomial formula.
    /// Lower values = slower speed growth. Default 0.5 means speed grows at half the rate of HP/Power.
    /// A value of ~0.073 makes enemies reach 1.0s action time around stage 900.
    /// </summary>
    public static double EnemySpeedScalingRate { get; private set; } = 0.5;

    /// <summary>
    /// Chance of beer drop after winning a battle (0.0 to 1.0)
    /// Default is 0.2 (20% chance)
    /// </summary>
    public static double BeerDropChance { get; private set; } = 0.2;

    public static void Configure(
        int baseLevel,
        int baseXp,
        int baseHp,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        double statMultiplierPerLevel,
        double statGrowthExponent,
        int xpPerLevelBase,
        int initialHpUpgrades,
        int initialPowerUpgrades,
        int initialSpeedUpgrades,
        int initialCriticalUpgrades,
        int initialDefenseUpgrades,
        double hpUpgradeMultiplier,
        double powerUpgradeMultiplier,
        double speedUpgradeMultiplier,
        double criticalChanceUpgradeMultiplier,
        double defenseUpgradeMultiplier,
        double defenseK,
        int minDamage,
        double beerDropChance = 0.2,
        double enemySpeedScalingRate = 0.5)
    {
        BaseLevel = baseLevel;
        BaseXp = baseXp;
        BaseHp = baseHp;
        BasePower = basePower;
        BaseSpeed = baseSpeed;
        BaseDefense = baseDefense;
        BaseCriticalChance = baseCriticalChance;
        StatMultiplierPerLevel = statMultiplierPerLevel;
        StatGrowthExponent = statGrowthExponent;
        XpPerLevelBase = xpPerLevelBase;
        InitialHpUpgrades = initialHpUpgrades;
        InitialPowerUpgrades = initialPowerUpgrades;
        InitialSpeedUpgrades = initialSpeedUpgrades;
        InitialCriticalUpgrades = initialCriticalUpgrades;
        InitialDefenseUpgrades = initialDefenseUpgrades;
        HpUpgradeMultiplier = hpUpgradeMultiplier;
        PowerUpgradeMultiplier = powerUpgradeMultiplier;
        SpeedUpgradeMultiplier = speedUpgradeMultiplier;
        CriticalChanceUpgradeMultiplier = criticalChanceUpgradeMultiplier;
        DefenseUpgradeMultiplier = defenseUpgradeMultiplier;
        DefenseK = defenseK;
        MinDamage = minDamage;
        BeerDropChance = beerDropChance;
        EnemySpeedScalingRate = enemySpeedScalingRate;
    }
}
