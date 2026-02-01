using System.ComponentModel.DataAnnotations;
using RTUB.Core.Configuration;
using RTUB.Core.Enums;

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

    // Base Stats (ONLY these three)
    public int HP { get; set; } = MyTunoScaling.BaseHp;        // Base HP
    public int Power { get; set; } = MyTunoScaling.BasePower;  // Base Power
    public int Speed { get; set; } = MyTunoScaling.BaseSpeed;  // Base Speed
    public double CriticalChance { get; set; } = MyTunoScaling.BaseCriticalChance;

    // Current HP (null means full HP, for backwards compatibility)
    public int? CurrentHP { get; set; } = null;

    // HP constants
    private const int MinHP = 0;

    // Upgrade Counts (for cost calculation)
    public int HpUpgrades { get; set; } = MyTunoScaling.InitialHpUpgrades;
    public int PowerUpgrades { get; set; } = MyTunoScaling.InitialPowerUpgrades;
    public int SpeedUpgrades { get; set; } = MyTunoScaling.InitialSpeedUpgrades;
    public int CriticalUpgrades { get; set; } = MyTunoScaling.InitialCriticalUpgrades;

    /// <summary>
    /// Currently equipped instrument (nullable, one at a time)
    /// </summary>
    public InventoryItemType? EquippedInstrument { get; set; }

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
            CriticalChance = MyTunoScaling.BaseCriticalChance,
            HpUpgrades = MyTunoScaling.InitialHpUpgrades,
            PowerUpgrades = MyTunoScaling.InitialPowerUpgrades,
            SpeedUpgrades = MyTunoScaling.InitialSpeedUpgrades,
            CriticalUpgrades = MyTunoScaling.InitialCriticalUpgrades
        };
    }

    /// <summary>
    /// Factory method to create a character with custom stats (for AI opponents)
    /// </summary>
    /// <param name="userId">User ID (can be system ID for AI)</param>
    /// <param name="nickname">Character nickname/name</param>
    /// <param name="level">Character level</param>
    /// <param name="hp">Base HP</param>
    /// <param name="power">Base Power</param>
    /// <param name="speed">Base Speed</param>
    /// <param name="criticalChance">Critical chance (0.0 to 1.0)</param>
    /// <returns>New character with custom stats</returns>
    public static Character Create(
        string userId,
        string nickname,
        int level,
        int hp,
        int power,
        int speed,
        double criticalChance)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));
        if (level < 1)
            throw new ArgumentException("Level must be at least 1", nameof(level));
        if (hp < 1)
            throw new ArgumentException("HP must be at least 1", nameof(hp));
        if (power < 0)
            throw new ArgumentException("Power cannot be negative", nameof(power));
        if (speed < 0)
            throw new ArgumentException("Speed cannot be negative", nameof(speed));
        if (criticalChance < 0 || criticalChance > 1)
            throw new ArgumentException("Critical chance must be between 0 and 1", nameof(criticalChance));

        // For custom characters, we set base stats directly without upgrades
        // This gives full control over the character's stats
        return new Character
        {
            UserId = userId,
            Level = level,
            XP = 0, // AI opponents don't gain XP
            HP = hp,
            Power = power,
            Speed = speed,
            CriticalChance = criticalChance,
            HpUpgrades = 0,
            PowerUpgrades = 0,
            SpeedUpgrades = 0,
            CriticalUpgrades = 0,
            CurrentHP = null // Start at full HP
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
    /// Sets HP to maximum
    /// </summary>
    public void RestoreHP()
    {
        CurrentHP = TotalHP;
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
    /// Equips or unequips an instrument
    /// </summary>
    /// <param name="instrumentType">The instrument type to equip, or null to unequip</param>
    public void EquipInstrument(InventoryItemType? instrumentType)
    {
        EquippedInstrument = instrumentType;
        UpdatedAt = DateTime.UtcNow;
    }
}
