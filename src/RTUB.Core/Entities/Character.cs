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

    // ── Equipped Items (null = empty slot) ──

    /// <summary>Equipment piece in head slot</summary>
    public InventoryItemType? EquippedHead { get; set; }
    /// <summary>Equipment piece in shoulders slot</summary>
    public InventoryItemType? EquippedShoulders { get; set; }
    /// <summary>Equipment piece in chest slot</summary>
    public InventoryItemType? EquippedChest { get; set; }
    /// <summary>Equipment piece in gloves slot</summary>
    public InventoryItemType? EquippedGloves { get; set; }
    /// <summary>Equipment piece in legs slot</summary>
    public InventoryItemType? EquippedLegs { get; set; }
    /// <summary>Equipment piece in boots slot</summary>
    public InventoryItemType? EquippedBoots { get; set; }
    /// <summary>Forged weapon in primary weapon slot (ID of ForgedWeapon entity)</summary>
    public int? EquippedWeapon1 { get; set; }
    /// <summary>Forged weapon in secondary weapon slot (ID of ForgedWeapon entity, null if two-handed weapon in slot 1)</summary>
    public int? EquippedWeapon2 { get; set; }

    // ── Equipment Stat Bonuses (recalculated on equip/unequip) ──

    /// <summary>Total HP bonus from all equipped items</summary>
    public int EquipmentHPBonus { get; set; }
    /// <summary>Total Power bonus from all equipped items</summary>
    public int EquipmentPowerBonus { get; set; }
    /// <summary>Total Speed bonus from all equipped items</summary>
    public int EquipmentSpeedBonus { get; set; }
    /// <summary>Total Defense bonus from all equipped items</summary>
    public int EquipmentDefenseBonus { get; set; }
    /// <summary>Total Critical chance bonus from all equipped items</summary>
    public double EquipmentCriticalBonus { get; set; }

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Computed properties (not stored in database)
    // Stats scale with level using polynomial growth:
    // stat = base * (1 + multiplier * (level-1)^(1+exponent)) + upgrades
    // When exponent=0 this reduces to simple linear scaling.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalHP => (int)(HP * LevelScaleFactor() * (1 + HpUpgrades * MyTunoScaling.HpUpgradeMultiplier))
        + EquipmentHPBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalPower => (int)(Power * LevelScaleFactor() * (1 + PowerUpgrades * MyTunoScaling.PowerUpgradeMultiplier))
        + EquipmentPowerBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalSpeed => (int)(Speed * LevelScaleFactor())
        + (int)(SpeedUpgrades * MyTunoScaling.SpeedUpgradeMultiplier);

    /// <summary>
    /// Maximum critical chance cap (50%)
    /// </summary>
    public const double MaxCriticalChance = 0.5;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double TotalCriticalChance =>
        Math.Min(MaxCriticalChance, CriticalChance + (CriticalUpgrades * MyTunoScaling.CriticalChanceUpgradeMultiplier));

    /// <summary>
    /// Effective defense base — self-heals characters whose Defense column is still 0
    /// from the original migration (defaultValue: 0). The switch from additive to multiplicative
    /// upgrade formulas made 0 * anything = 0, so we fall back to config base.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    private int EffectiveDefense => Defense > 0 ? Defense : MyTunoScaling.BaseDefense;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalDefense => (int)(EffectiveDefense * LevelScaleFactor() * (1 + DefenseUpgrades * MyTunoScaling.DefenseUpgradeMultiplier))
        + EquipmentDefenseBonus;

    // Preview properties: what the stat will be after the next upgrade
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int NextTotalHP => (int)(HP * LevelScaleFactor() * (1 + (HpUpgrades + 1) * MyTunoScaling.HpUpgradeMultiplier))
        + EquipmentHPBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int NextTotalPower => (int)(Power * LevelScaleFactor() * (1 + (PowerUpgrades + 1) * MyTunoScaling.PowerUpgradeMultiplier))
        + EquipmentPowerBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int NextTotalDefense => (int)(EffectiveDefense * LevelScaleFactor() * (1 + (DefenseUpgrades + 1) * MyTunoScaling.DefenseUpgradeMultiplier))
        + EquipmentDefenseBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double NextTotalCriticalChance =>
        Math.Min(MaxCriticalChance, CriticalChance + ((CriticalUpgrades + 1) * MyTunoScaling.CriticalChanceUpgradeMultiplier));

    /// <summary>
    /// Computes the level-based stat multiplier using polynomial growth.
    /// Formula: 1 + multiplier * (level-1)^(1+exponent)
    /// When exponent=0 this is simple linear: 1 + multiplier * (level-1)
    /// </summary>
    private double LevelScaleFactor()
    {
        var levelsGained = Level - 1;
        if (levelsGained <= 0) return 1.0;
        var exponent = MyTunoScaling.StatGrowthExponent;
        if (exponent == 0.0)
            return 1.0 + levelsGained * MyTunoScaling.StatMultiplierPerLevel;
        return 1.0 + MyTunoScaling.StatMultiplierPerLevel * Math.Pow(levelsGained, 1.0 + exponent);
    }

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
            DefenseUpgrades = MyTunoScaling.InitialDefenseUpgrades,
            // No equipment equipped by default
            EquippedHead = null,
            EquippedShoulders = null,
            EquippedChest = null,
            EquippedGloves = null,
            EquippedLegs = null,
            EquippedBoots = null,
            EquippedWeapon1 = null,
            EquippedWeapon2 = null,
            EquipmentHPBonus = 0,
            EquipmentPowerBonus = 0,
            EquipmentSpeedBonus = 0,
            EquipmentDefenseBonus = 0,
            EquipmentCriticalBonus = 0
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
            // Carry over equipment bonuses
            EquippedHead = source.EquippedHead,
            EquippedShoulders = source.EquippedShoulders,
            EquippedChest = source.EquippedChest,
            EquippedGloves = source.EquippedGloves,
            EquippedLegs = source.EquippedLegs,
            EquippedBoots = source.EquippedBoots,
            EquippedWeapon1 = source.EquippedWeapon1,
            EquippedWeapon2 = source.EquippedWeapon2,
            EquipmentHPBonus = source.EquipmentHPBonus,
            EquipmentPowerBonus = source.EquipmentPowerBonus,
            EquipmentSpeedBonus = source.EquipmentSpeedBonus,
            EquipmentDefenseBonus = source.EquipmentDefenseBonus,
            EquipmentCriticalBonus = source.EquipmentCriticalBonus,
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
            // Carry over equipment bonuses (not buffed — they are flat bonuses)
            EquippedHead = source.EquippedHead,
            EquippedShoulders = source.EquippedShoulders,
            EquippedChest = source.EquippedChest,
            EquippedGloves = source.EquippedGloves,
            EquippedLegs = source.EquippedLegs,
            EquippedBoots = source.EquippedBoots,
            EquippedWeapon1 = source.EquippedWeapon1,
            EquippedWeapon2 = source.EquippedWeapon2,
            EquipmentHPBonus = source.EquipmentHPBonus,
            EquipmentPowerBonus = source.EquipmentPowerBonus,
            EquipmentSpeedBonus = source.EquipmentSpeedBonus,
            EquipmentDefenseBonus = source.EquipmentDefenseBonus,
            EquipmentCriticalBonus = source.EquipmentCriticalBonus,
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
