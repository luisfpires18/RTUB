namespace RTUB.Application.Configuration;

public class MyTunoScalingConfiguration
{
    public const string SectionName = "myTuno";

    public MyTunoBaseStats BaseStats { get; set; } = new();
    public MyTunoLevelScaling LevelScaling { get; set; } = new();
    public MyTunoUpgrades Upgrades { get; set; } = new();
    public List<decimal> LevelCosts { get; set; } = new();
    public BattleRewards BattleRewards { get; set; } = new();
    
    /// <summary>
    /// Chance of beer drop after winning a battle (0.0 to 1.0)
    /// Default is 0.5 (50% chance)
    /// </summary>
    public double BeerDropChance { get; set; } = 0.5;
}

public class BattleRewards
{
    public decimal WinReward { get; set; } = 10m;
    public decimal DrawReward { get; set; } = 7.5m;
    
    /// <summary>
    /// XP scaling based on level difference
    /// Formula: XP = BaseXP * (1.0 + (defenderLevel - attackerLevel) * XpScalingFactor)
    /// </summary>
    public double XpScalingFactor { get; set; } = 0.05; // 5% per level difference
    
    /// <summary>
    /// Minimum XP multiplier (prevents too little XP from weak opponents)
    /// Default 0.2 means minimum 20% of base XP
    /// </summary>
    public double MinXpMultiplier { get; set; } = 0.2;
    
    /// <summary>
    /// Maximum XP multiplier (prevents too much XP from strong opponents)
    /// Default 3.0 means maximum 300% of base XP
    /// </summary>
    public double MaxXpMultiplier { get; set; } = 3.0;
    
    /// <summary>
    /// Cost in Fidelis to revive a defeated character
    /// Default is 100 Fidelis
    /// </summary>
    public decimal ReviveCost { get; set; } = 100m;
}

public class MyTunoBaseStats
{
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public int HP { get; set; } = 100;
    public int Power { get; set; } = 10;
    public int Speed { get; set; } = 10;
    public double CriticalChance { get; set; } = 0.01;
}

public class MyTunoLevelScaling
{
    public double StatMultiplierPerLevel { get; set; } = 0.1;
    public int XpPerLevelBase { get; set; } = 100;
    public string XpToNextLevelFormula { get; set; } = "level * 100";
}

public class MyTunoUpgrades
{
    public MyTunoUpgradeStat HP { get; set; } = new();
    public MyTunoUpgradeStat Power { get; set; } = new();
    public MyTunoUpgradeStat Speed { get; set; } = new();
    public MyTunoUpgradeStat CriticalChance { get; set; } = new();
}

public class MyTunoUpgradeStat
{
    public int InitialBought { get; set; }
    public double BonusPerUpgrade { get; set; }
    public decimal BaseCost { get; set; }
}
