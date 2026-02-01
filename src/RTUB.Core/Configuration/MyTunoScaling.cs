namespace RTUB.Core.Configuration;

public static class MyTunoScaling
{
    public static int BaseLevel { get; private set; } = 1;
    public static int BaseXp { get; private set; } = 0;
    public static int BaseHp { get; private set; } = 100;
    public static int BasePower { get; private set; } = 10;
    public static int BaseSpeed { get; private set; } = 10;
    public static double BaseCriticalChance { get; private set; } = 0.01;

    public static double StatMultiplierPerLevel { get; private set; } = 0.1;
    public static int XpPerLevelBase { get; private set; } = 100;

    public static int InitialHpUpgrades { get; private set; } = 0;
    public static int InitialPowerUpgrades { get; private set; } = 0;
    public static int InitialSpeedUpgrades { get; private set; } = 0;
    public static int InitialCriticalUpgrades { get; private set; } = 0;

    public static double HpUpgradeBonus { get; private set; } = 10;
    public static double PowerUpgradeBonus { get; private set; } = 2;
    public static double SpeedUpgradeBonus { get; private set; } = 1;
    public static double CriticalChanceUpgradeBonus { get; private set; } = 0.005;

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
        double baseCriticalChance,
        double statMultiplierPerLevel,
        int xpPerLevelBase,
        int initialHpUpgrades,
        int initialPowerUpgrades,
        int initialSpeedUpgrades,
        int initialCriticalUpgrades,
        double hpUpgradeBonus,
        double powerUpgradeBonus,
        double speedUpgradeBonus,
        double criticalChanceUpgradeBonus,
        double beerDropChance = 0.2)
    {
        BaseLevel = baseLevel;
        BaseXp = baseXp;
        BaseHp = baseHp;
        BasePower = basePower;
        BaseSpeed = baseSpeed;
        BaseCriticalChance = baseCriticalChance;
        StatMultiplierPerLevel = statMultiplierPerLevel;
        XpPerLevelBase = xpPerLevelBase;
        InitialHpUpgrades = initialHpUpgrades;
        InitialPowerUpgrades = initialPowerUpgrades;
        InitialSpeedUpgrades = initialSpeedUpgrades;
        InitialCriticalUpgrades = initialCriticalUpgrades;
        HpUpgradeBonus = hpUpgradeBonus;
        PowerUpgradeBonus = powerUpgradeBonus;
        SpeedUpgradeBonus = speedUpgradeBonus;
        CriticalChanceUpgradeBonus = criticalChanceUpgradeBonus;
        BeerDropChance = beerDropChance;
    }
}
