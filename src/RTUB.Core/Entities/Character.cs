using System.ComponentModel.DataAnnotations;
using RTUB.Core.Configuration;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's character in My Tuno
/// Main persistent entity for the game
/// </summary>
public class Character : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Progression
    public int Level { get; set; } = MyTunoScaling.BaseLevel;
    public int XP { get; set; } = MyTunoScaling.BaseXp;

    // Base Stats (ONLY these four)
    public int HP { get; set; } = MyTunoScaling.BaseHp;        // Base HP
    public int Power { get; set; } = MyTunoScaling.BasePower;  // Base Power
    public int Speed { get; set; } = MyTunoScaling.BaseSpeed;  // Base Speed
    public int Defense { get; set; } = MyTunoScaling.BaseDefense; // Base Defense
    public double CriticalChance { get; set; } = MyTunoScaling.BaseCriticalChance;

    // Current HP (null means full HP, for backwards compatibility)
    public int? CurrentHP { get; set; } = null;

    // Shot buff - number of arena battles remaining with empowerment
    public int ShotBuffBattlesRemaining { get; set; } = 0;

    // HP constants
    private const int MinHP = 0;

    // Upgrade Counts (for cost calculation)
    public int HpUpgrades { get; set; } = MyTunoScaling.InitialHpUpgrades;
    public int PowerUpgrades { get; set; } = MyTunoScaling.InitialPowerUpgrades;
    public int SpeedUpgrades { get; set; } = MyTunoScaling.InitialSpeedUpgrades;
    public int CriticalUpgrades { get; set; } = MyTunoScaling.InitialCriticalUpgrades;
    public int DefenseUpgrades { get; set; } = MyTunoScaling.InitialDefenseUpgrades;

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Computed properties (not stored in database)
    // Stats scale with level: base stats increase by 10% per level
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalHP => (int)(HP * (1 + (Level - 1) * MyTunoScaling.StatMultiplierPerLevel))
        + (int)(HpUpgrades * MyTunoScaling.HpUpgradeBonus);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalPower => (int)(Power * (1 + (Level - 1) * MyTunoScaling.StatMultiplierPerLevel))
        + (int)(PowerUpgrades * MyTunoScaling.PowerUpgradeBonus);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalSpeed => (int)(Speed * (1 + (Level - 1) * MyTunoScaling.StatMultiplierPerLevel))
        + (int)(SpeedUpgrades * MyTunoScaling.SpeedUpgradeBonus);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double TotalCriticalChance =>
        Math.Min(1, CriticalChance + (CriticalUpgrades * MyTunoScaling.CriticalChanceUpgradeBonus));

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalDefense => (int)(Defense * (1 + (Level - 1) * MyTunoScaling.StatMultiplierPerLevel))
        + (int)(DefenseUpgrades * MyTunoScaling.DefenseUpgradeBonus);

    /// <summary>
    /// Base action time in seconds (how long before a character can attack)
    /// </summary>
    public const double BaseActionTime = 5.0;
    
    /// <summary>
    /// Minimum action time in seconds (cannot go below this)
    /// </summary>
    public const double MinActionTime = 1.0;
    
    /// <summary>
    /// Time reduction per speed upgrade in seconds
    /// </summary>
    public const double ActionTimeReductionPerUpgrade = 0.1;

    /// <summary>
    /// Calculates the action time in seconds based on speed upgrades
    /// Base time is 5 seconds, each speed upgrade reduces by 0.1 seconds
    /// Minimum is 1 second
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double ActionTime => Math.Max(MinActionTime, BaseActionTime - (SpeedUpgrades * ActionTimeReductionPerUpgrade));

    // Private constructor for EF Core
    private Character() { }

    /// <summary>
    /// Factory method to create a new character for a user
    /// </summary>
    public static Character Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new Character
        {
            UserId = userId,
            Level = MyTunoScaling.BaseLevel,
            XP = MyTunoScaling.BaseXp,
            HP = MyTunoScaling.BaseHp,
            Power = MyTunoScaling.BasePower,
            Speed = MyTunoScaling.BaseSpeed,
            Defense = MyTunoScaling.BaseDefense,
            CriticalChance = MyTunoScaling.BaseCriticalChance,
            HpUpgrades = MyTunoScaling.InitialHpUpgrades,
            PowerUpgrades = MyTunoScaling.InitialPowerUpgrades,
            SpeedUpgrades = MyTunoScaling.InitialSpeedUpgrades,
            CriticalUpgrades = MyTunoScaling.InitialCriticalUpgrades,
            DefenseUpgrades = MyTunoScaling.InitialDefenseUpgrades
        };
    }

    /// <summary>
    /// Factory method to create a stage enemy character (for combat simulation only, not persisted)
    /// </summary>
    /// <param name="hp">Base HP of the enemy</param>
    /// <param name="power">Base power of the enemy</param>
    /// <param name="speed">Base speed of the enemy</param>
    /// <param name="defense">Base defense of the enemy</param>
    /// <param name="criticalChance">Critical hit chance</param>
    /// <param name="enemyName">Display name for the enemy</param>
    /// <returns>A Character instance for combat simulation</returns>
    public static Character CreateStageEnemy(int hp, int power, int speed, int defense, double criticalChance, string enemyName)
    {
        var character = new Character
        {
            UserId = "stage-enemy",
            Level = 1,
            XP = 0,
            HP = hp,
            Power = power,
            Speed = speed,
            Defense = defense,
            CriticalChance = criticalChance,
            CurrentHP = hp,
            HpUpgrades = 0,
            PowerUpgrades = 0,
            SpeedUpgrades = 0,
            CriticalUpgrades = 0,
            DefenseUpgrades = 0
        };

        // Create a temporary user with the enemy name
        character.User = new ApplicationUser { UserName = enemyName, Nickname = enemyName };

        return character;
    }

    /// <summary>
    /// Creates a CPU snapshot of an existing character for arena battles
    /// The snapshot has the same build (stats/gear) but starts at full HP
    /// Used when another player's character acts as CPU opponent
    /// </summary>
    /// <param name="source">The character to create a snapshot from</param>
    /// <returns>A new Character instance with full HP</returns>
    public static Character CreateCpuSnapshot(Character source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return new Character
        {
            Id = source.Id,
            UserId = source.UserId,
            Level = source.Level,
            XP = source.XP,
            HP = source.HP,
            Power = source.Power,
            Speed = source.Speed,
            Defense = source.Defense,
            CriticalChance = source.CriticalChance,
            HpUpgrades = source.HpUpgrades,
            PowerUpgrades = source.PowerUpgrades,
            SpeedUpgrades = source.SpeedUpgrades,
            CriticalUpgrades = source.CriticalUpgrades,
            DefenseUpgrades = source.DefenseUpgrades,
            // Key: Set CurrentHP to null = full HP (TotalHP)
            CurrentHP = null,
            User = source.User,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    /// <summary>
    /// Creates a combat-ready copy with shot buff applied (+20% to all stats)
    /// Used for arena battles when shot buff is active
    /// The copy preserves the character's current HP and uses buffed max HP
    /// </summary>
    /// <param name="source">The character to buff</param>
    /// <returns>A new Character instance with boosted stats for combat simulation</returns>
    public static Character CreateShotBuffedCopy(Character source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        const double buffMultiplier = 1.20; // 20% boost

        // Simple approach: multiply the base HP stat by 1.2
        // This makes TotalHP automatically scale up (though not exactly 1.2x due to upgrades)
        // But we also scale the upgrade bonus by storing extra "virtual" upgrades
        var buffedHP = (int)Math.Round(source.HP * buffMultiplier);
        var buffedHpUpgrades = (int)Math.Round(source.HpUpgrades * buffMultiplier);

        return new Character
        {
            Id = source.Id,
            UserId = source.UserId,
            Level = source.Level,
            XP = source.XP,
            // Boost base HP stat by 20%
            HP = buffedHP,
            // Boost all other stats by 20%
            Power = (int)Math.Round(source.Power * buffMultiplier),
            Speed = (int)Math.Round(source.Speed * buffMultiplier),
            Defense = (int)Math.Round(source.Defense * buffMultiplier),
            CriticalChance = Math.Min(1.0, source.CriticalChance * buffMultiplier),
            // Also scale HP upgrades so total HP is exactly 1.2x
            HpUpgrades = buffedHpUpgrades,
            PowerUpgrades = source.PowerUpgrades,
            SpeedUpgrades = source.SpeedUpgrades,
            CriticalUpgrades = source.CriticalUpgrades,
            DefenseUpgrades = source.DefenseUpgrades,
            // Preserve current HP so combat starts with actual HP (damaged or full)
            CurrentHP = source.CurrentHP,
            User = source.User,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            ShotBuffBattlesRemaining = source.ShotBuffBattlesRemaining
        };
    }

    /// <summary>
    /// Adds XP to the character and handles level-ups
    /// Level up formula: Each level requires 100 * level XP to reach the next level
    /// Level 1->2: 100 XP, Level 2->3: 200 XP, Level 3->4: 300 XP, etc.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("XP amount cannot be negative", nameof(amount));

        XP += amount;

        // Level up logic: Each level requires (Level * 100) XP to reach the next level
        // Level 1 needs 100 XP to become Level 2
        // Level 2 needs 200 XP to become Level 3
        // Level 3 needs 300 XP to become Level 4, etc.
        while (XP >= Level * MyTunoScaling.XpPerLevelBase)
        {
            XP -= Level * MyTunoScaling.XpPerLevelBase;
            Level++;
            // Heal to full HP on level-up
            CurrentHP = TotalHP;
        }
    }

    /// <summary>
    /// Upgrades HP stat (increments HpUpgrades count)
    /// </summary>
    public void UpgradeHP()
    {
        HpUpgrades++;
    }

    /// <summary>
    /// Upgrades Power stat (increments PowerUpgrades count)
    /// </summary>
    public void UpgradePower()
    {
        PowerUpgrades++;
    }

    /// <summary>
    /// Upgrades Speed stat (increments SpeedUpgrades count)
    /// </summary>
    public void UpgradeSpeed()
    {
        SpeedUpgrades++;
    }

    /// <summary>
    /// Upgrades Critical chance stat (increments CriticalUpgrades count)
    /// </summary>
    public void UpgradeCriticalChance()
    {
        CriticalUpgrades++;
    }

    /// <summary>
    /// Upgrades Defense stat (increments DefenseUpgrades count)
    /// </summary>
    public void UpgradeDefense()
    {
        DefenseUpgrades++;
    }

    /// <summary>
    /// Takes damage and updates CurrentHP
    /// </summary>
    public void TakeDamage(int damage)
    {
        var currentHp = CurrentHP ?? TotalHP;
        currentHp -= damage;
        CurrentHP = Math.Max(MinHP, currentHp);
    }

    /// <summary>
    /// Heals the character and updates CurrentHP
    /// </summary>
    public void Heal(int amount)
    {
        var currentHp = CurrentHP ?? TotalHP;
        currentHp += amount;
        CurrentHP = Math.Min(TotalHP, currentHp);
    }

    /// <summary>
    /// Sets HP to maximum (accounts for shot buff if active)
    /// </summary>
    public void RestoreHP()
    {
        var maxHP = ShotBuffBattlesRemaining > 0 
            ? CreateShotBuffedCopy(this).TotalHP 
            : TotalHP;
        CurrentHP = maxHP;
    }

    /// <summary>
    /// Checks if character is alive
    /// </summary>
    public bool IsAlive()
    {
        var currentHp = CurrentHP ?? TotalHP;
        return currentHp > MinHP;
    }
}
