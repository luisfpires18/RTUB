namespace RTUB.Core.Configuration;

public static class MyTunoScaling
{
    public static int BaseLevel { get; private set; } = 1;
    public static int BaseXp { get; private set; } = 0;
    public static int BaseHp { get; private set; } = 100;
    public static int BasePower { get; private set; } = 10;
    public static int BaseSpeed { get; private set; } = 10;

    public static double StatMultiplierPerLevel { get; private set; } = 0.1;
    public static int XpPerLevelBase { get; private set; } = 100;

    public static int InitialHpUpgrades { get; private set; } = 0;
    public static int InitialPowerUpgrades { get; private set; } = 0;
    public static int InitialSpeedUpgrades { get; private set; } = 0;

    public static int HpUpgradeBonus { get; private set; } = 10;
    public static int PowerUpgradeBonus { get; private set; } = 2;
    public static int SpeedUpgradeBonus { get; private set; } = 1;

    public static void Configure(
        int baseLevel,
        int baseXp,
        int baseHp,
        int basePower,
        int baseSpeed,
        double statMultiplierPerLevel,
        int xpPerLevelBase,
        int initialHpUpgrades,
        int initialPowerUpgrades,
        int initialSpeedUpgrades,
        int hpUpgradeBonus,
        int powerUpgradeBonus,
        int speedUpgradeBonus)
    {
        BaseLevel = baseLevel;
        BaseXp = baseXp;
        BaseHp = baseHp;
        BasePower = basePower;
        BaseSpeed = baseSpeed;
        StatMultiplierPerLevel = statMultiplierPerLevel;
        XpPerLevelBase = xpPerLevelBase;
        InitialHpUpgrades = initialHpUpgrades;
        InitialPowerUpgrades = initialPowerUpgrades;
        InitialSpeedUpgrades = initialSpeedUpgrades;
        HpUpgradeBonus = hpUpgradeBonus;
        PowerUpgradeBonus = powerUpgradeBonus;
        SpeedUpgradeBonus = speedUpgradeBonus;
    }
}
