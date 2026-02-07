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

    // Shot buff - number of battles remaining with empowerment
    public int ShotBuffBattlesRemaining { get; set; } = 0;

    // Arena Statistics
    /// <summary>
    /// Total number of arena wins
    /// </summary>
    public int ArenaWins { get; set; } = 0;

    /// <summary>
    /// Total number of arena losses
    /// </summary>
    public int ArenaLosses { get; set; } = 0;

    /// <summary>
    /// Total number of arena draws
    /// </summary>
    public int ArenaDraws { get; set; } = 0;

    /// <summary>
    /// Last opponent character ID (for cooldown tracking)
    /// </summary>
    public int? LastOpponentId { get; set; } = null;

    /// <summary>
    /// Timestamp of the last arena battle (for cooldown tracking)
    /// </summary>
    public DateTime? LastBattleAt { get; set; } = null;

    // Energy system (for resource gathering)
    /// <summary>
    /// Current stored energy for gathering resources
    /// </summary>
    public int Energy { get; set; } = 10;

    /// <summary>
    /// Maximum energy capacity
    /// </summary>
    public int MaxEnergy { get; set; } = 10;

    /// <summary>
    /// Timestamp of the last energy regeneration tick (for calculating passive regen)
    /// </summary>
    public DateTime? LastEnergyRegenAt { get; set; } = null;

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

    /// <summary>
    /// Maximum critical chance cap (50%)
    /// </summary>
    public const double MaxCriticalChance = 0.5;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double TotalCriticalChance =>
        Math.Min(MaxCriticalChance, CriticalChance + (CriticalUpgrades * MyTunoScaling.CriticalChanceUpgradeBonus));

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
    /// Time reduction per speed upgrade in seconds.
    /// At 40 upgrades: 40 × 0.075 = 3.0s reduction from upgrades alone.
    /// Combined with TotalSpeed scaling, reaches 1.0s minimum at max upgrades.
    /// </summary>
    public const double ActionTimeReductionPerUpgrade = 0.075;

    /// <summary>
    /// Seconds of action time reduced per point of TotalSpeed.
    /// Provides a small but meaningful speed scaling from the Speed stat itself,
    /// so enemies with high scaled speed (from higher stages) attack faster.
    /// At TotalSpeed=50 this provides 1.0s reduction.
    /// </summary>
    public const double ActionTimeReductionPerSpeedPoint = 0.02;

    /// <summary>
    /// Calculates the action time in seconds.
    /// Two sources of speed reduction:
    /// 1) TotalSpeed stat: -0.03s per point (enemies scale this via stages, players via levels)
    /// 2) SpeedUpgrades: -0.1s per upgrade (player-only flat reduction from shop purchases)
    /// Minimum is 1 second.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double ActionTime
    {
        get
        {
            var time = BaseActionTime;
            
            // Speed stat provides a small per-point reduction (meaningful for enemies at higher stages)
            time -= TotalSpeed * ActionTimeReductionPerSpeedPoint;
            
            // Speed upgrades provide the main flat reduction for players
            time -= SpeedUpgrades * ActionTimeReductionPerUpgrade;
            
            return Math.Max(MinActionTime, time);
        }
    }

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
    /// If the character was at full HP before the upgrade, heals to the new max HP
    /// Accounts for shot buff when determining full HP
    /// </summary>
    public void UpgradeHP()
    {
        var maxHP = ShotBuffBattlesRemaining > 0
            ? CreateShotBuffedCopy(this).TotalHP
            : TotalHP;
        var wasAtFullHp = CurrentHP == null || CurrentHP >= maxHP;
        HpUpgrades++;
        if (wasAtFullHp)
        {
            // Recalculate buffed max after upgrade
            var newMaxHP = ShotBuffBattlesRemaining > 0
                ? CreateShotBuffedCopy(this).TotalHP
                : TotalHP;
            CurrentHP = newMaxHP;
        }
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
    /// Accounts for shot buff if active
    /// </summary>
    public void Heal(int amount)
    {
        var maxHP = ShotBuffBattlesRemaining > 0 
            ? CreateShotBuffedCopy(this).TotalHP 
            : TotalHP;
        var currentHp = CurrentHP ?? maxHP;
        currentHp += amount;
        CurrentHP = Math.Min(maxHP, currentHp);
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

    /// <summary>
    /// Regenerates energy based on time elapsed since last regen.
    /// 1 energy per second, capped at MaxEnergy.
    /// Returns the updated energy value.
    /// </summary>
    public int RegenerateEnergy()
    {
        if (LastEnergyRegenAt == null)
        {
            LastEnergyRegenAt = DateTime.UtcNow;
            return Energy;
        }

        var elapsed = DateTime.UtcNow - LastEnergyRegenAt.Value;
        var regenAmount = (int)elapsed.TotalSeconds;

        if (regenAmount > 0)
        {
            Energy = Math.Min(MaxEnergy, Energy + regenAmount);
            LastEnergyRegenAt = DateTime.UtcNow;
        }

        return Energy;
    }

    /// <summary>
    /// Spends energy for gathering. Returns true if successful.
    /// </summary>
    public bool SpendEnergy(int amount)
    {
        RegenerateEnergy();

        if (Energy < amount)
            return false;

        Energy -= amount;
        LastEnergyRegenAt = DateTime.UtcNow;
        return true;
    }
}
