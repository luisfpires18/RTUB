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
    public long? CurrentHP { get; set; } = null;

    // Shot buff - number of battles remaining with empowerment
    public int ShotBuffBattlesRemaining { get; set; } = 0;

    // Cigarro buff - number of hits remaining where damage is absorbed
    public int CigarroShieldHitsRemaining { get; set; } = 0;

    // Canhão buff - number of hits remaining where damage is boosted by 30%
    public int CanhaoDamageBoostHitsRemaining { get; set; } = 0;

    // Penalty buff - reduces action time by 0.5s + adds 50% crit chance for 1 run/battle
    public int PenaltyBuffActive { get; set; } = 0;

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

    /// <summary>
    /// Idempotency key — the BattleId of the last finalized arena battle.
    /// Prevents duplicate reward application if the client calls FinalizeAndApplyRewardsAsync twice.
    /// </summary>
    public Guid? LastBattleId { get; set; } = null;

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
    public int HpUpgrades { get; set; }
    public int PowerUpgrades { get; set; }
    public int SpeedUpgrades { get; set; }
    public int CriticalUpgrades { get; set; }
    public int DefenseUpgrades { get; set; }

    // ── Improvements (game-wide improvements) ──

    /// <summary>Number of energy capacity upgrades purchased</summary>
    public int EnergyAmountUpgrades { get; set; }

    /// <summary>Number of energy regeneration upgrades purchased</summary>
    public int EnergyRegenUpgrades { get; set; }

    /// <summary>Number of shot buff bonus upgrades purchased</summary>
    public int ShotBuffUpgrades { get; set; }

    /// <summary>Number of fidelis earned bonus upgrades purchased</summary>
    public int FidelisEarnedUpgrades { get; set; }

    // ── Powers (combat power enhancements) ──

    /// <summary>Number of heavy attack damage upgrades purchased</summary>
    public int HeavyAttackUpgrades { get; set; }

    /// <summary>Number of special attack damage upgrades purchased</summary>
    public int SpecialAttackUpgrades { get; set; }

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

    // ── Per-Slot Quality (randomized on equip, range ~0.85–1.15) ──

    /// <summary>Quality multiplier for equipped head piece (0 = use average)</summary>
    public double EquippedHeadQuality { get; set; }
    /// <summary>Quality multiplier for equipped shoulders piece</summary>
    public double EquippedShouldersQuality { get; set; }
    /// <summary>Quality multiplier for equipped chest piece</summary>
    public double EquippedChestQuality { get; set; }
    /// <summary>Quality multiplier for equipped gloves piece</summary>
    public double EquippedGlovesQuality { get; set; }
    /// <summary>Quality multiplier for equipped legs piece</summary>
    public double EquippedLegsQuality { get; set; }
    /// <summary>Quality multiplier for equipped boots piece</summary>
    public double EquippedBootsQuality { get; set; }

    // ── Per-Slot Bonus Level (purchased upgrades per equipment slot) ──

    /// <summary>Purchased upgrade level for the head slot</summary>
    public int EquippedHeadBonusLevel { get; set; }
    /// <summary>Purchased upgrade level for the shoulders slot</summary>
    public int EquippedShouldersBonusLevel { get; set; }
    /// <summary>Purchased upgrade level for the chest slot</summary>
    public int EquippedChestBonusLevel { get; set; }
    /// <summary>Purchased upgrade level for the gloves slot</summary>
    public int EquippedGlovesBonusLevel { get; set; }
    /// <summary>Purchased upgrade level for the legs slot</summary>
    public int EquippedLegsBonusLevel { get; set; }
    /// <summary>Purchased upgrade level for the boots slot</summary>
    public int EquippedBootsBonusLevel { get; set; }

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

    /// <summary>Character's visual customization (one-to-one)</summary>
    public virtual CharacterAppearance? Appearance { get; set; }

    // Computed properties (not stored in database)
    // Stats scale with level (polynomial) × upgrades (compound exponential):
    // stat = base × levelScale × (1 + mult)^upgrades + equipment
    // Each upgrade multiplies the stat by a fixed factor — absolute gains grow with each one.
    // Math.Round avoids truncation bias that causes non-monotonic marginal upgrade gains.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long TotalHP => (long)Math.Round(HP * LevelScaleFactor() * Math.Pow(1 + MyTunoScaling.HpUpgradeMultiplier, HpUpgrades))
        + EquipmentHPBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long TotalPower => (long)Math.Round(Power * LevelScaleFactor() * Math.Pow(1 + MyTunoScaling.PowerUpgradeMultiplier, PowerUpgrades))
        + EquipmentPowerBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long TotalSpeed => (long)Math.Round(Speed * LevelScaleFactor())
        + (long)Math.Round(SpeedUpgrades * MyTunoScaling.SpeedUpgradeMultiplier)
        + EquipmentSpeedBonus;

    /// <summary>
    /// Maximum critical chance cap (50%)
    /// </summary>
    public const double MaxCriticalChance = 0.5;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double TotalCriticalChance
    {
        get
        {
            var raw = CriticalChance + (CriticalUpgrades * MyTunoScaling.CriticalChanceUpgradeMultiplier);
            // Penalty buff explicitly allows up to 100% crit — skip the normal 50% cap
            var upgradeCrit = PenaltyBuffActive > 0 ? Math.Min(1.0, raw) : Math.Min(MaxCriticalChance, raw);
            // Equipment crit bonus stacks on top of upgrade-capped value (absolute cap 100%)
            return Math.Min(1.0, upgradeCrit + EquipmentCriticalBonus);
        }
    }

    /// <summary>
    /// Effective defense base — self-heals characters whose Defense column is still 0
    /// from the original migration (defaultValue: 0). The switch from additive to multiplicative
    /// upgrade formulas made 0 * anything = 0, so we fall back to config base.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    private int EffectiveDefense => Defense > 0 ? Defense : MyTunoScaling.BaseDefense;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long TotalDefense => (long)Math.Round(EffectiveDefense * DefenseLevelScaleFactor() * Math.Pow(1 + MyTunoScaling.DefenseUpgradeMultiplier, DefenseUpgrades))
        + EquipmentDefenseBonus;

    // Preview properties: what the stat will be after the next upgrade
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long NextTotalHP => (long)Math.Round(HP * LevelScaleFactor() * Math.Pow(1 + MyTunoScaling.HpUpgradeMultiplier, HpUpgrades + 1))
        + EquipmentHPBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long NextTotalPower => (long)Math.Round(Power * LevelScaleFactor() * Math.Pow(1 + MyTunoScaling.PowerUpgradeMultiplier, PowerUpgrades + 1))
        + EquipmentPowerBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long NextTotalDefense => (long)Math.Round(EffectiveDefense * DefenseLevelScaleFactor() * Math.Pow(1 + MyTunoScaling.DefenseUpgradeMultiplier, DefenseUpgrades + 1))
        + EquipmentDefenseBonus;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double NextTotalCriticalChance =>
        Math.Min(1.0, Math.Min(MaxCriticalChance, CriticalChance + ((CriticalUpgrades + 1) * MyTunoScaling.CriticalChanceUpgradeMultiplier)) + EquipmentCriticalBonus);

    // ── Improvements computed properties ──

    /// <summary>
    /// Effective maximum energy including upgrades.
    /// Each upgrade adds EnergyAmountPerUpgrade to the base MaxEnergy.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int EffectiveMaxEnergy => MyTunoScaling.BaseMaxEnergy + (int)(EnergyAmountUpgrades * MyTunoScaling.EnergyAmountPerUpgrade);

    /// <summary>
    /// Energy regeneration interval in seconds, reduced by regen upgrades.
    /// Each upgrade reduces the interval by RegenReductionPerUpgrade seconds (min 5s).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double EffectiveRegenInterval => Math.Max(5.0, MyTunoScaling.BaseRegenInterval - EnergyRegenUpgrades * MyTunoScaling.RegenReductionPerUpgrade);

    /// <summary>
    /// Effective shot buff multiplier including upgrades.
    /// Each upgrade adds ShotBuffBonusPerUpgrade (0.005 = +0.5%) to the base multiplier.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double EffectiveShotBuffMultiplier => MyTunoScaling.ShotBuffMultiplier + ShotBuffUpgrades * MyTunoScaling.ShotBuffBonusPerUpgrade;

    /// <summary>
    /// Fidelis earned bonus multiplier (1.0 = no bonus, 1.05 = +5%).
    /// Each upgrade adds FidelisEarnedBonusPerUpgrade to 1.0.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double FidelisEarnedMultiplier => 1.0 + FidelisEarnedUpgrades * MyTunoScaling.FidelisEarnedBonusPerUpgrade;

    // ── Powers computed properties ──

    /// <summary>
    /// Heavy attack damage bonus multiplier (additive on top of base 2.0x).
    /// Each upgrade adds HeavyAttackBonusPerUpgrade.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double HeavyAttackDamageBonus => HeavyAttackUpgrades * MyTunoScaling.HeavyAttackBonusPerUpgrade;

    /// <summary>
    /// Special attack damage bonus multiplier (additive on top of base).
    /// Each upgrade adds SpecialAttackBonusPerUpgrade.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double SpecialAttackDamageBonus => SpecialAttackUpgrades * MyTunoScaling.SpecialAttackBonusPerUpgrade;

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
    /// Defense uses sqrt of the main scale factor so it grows much slower
    /// than offensive stats, preventing late-game damage stalemates.
    /// </summary>
    private double DefenseLevelScaleFactor() => Math.Sqrt(LevelScaleFactor());

    /// <summary>
    /// Base action time in seconds (how long before a character can attack)
    /// </summary>
    public const double BaseActionTime = 5.0;
    
    /// <summary>
    /// Minimum action time from upgrades alone (cannot go below this without equipment)
    /// </summary>
    public const double MinActionTime = 1.0;

    /// <summary>
    /// Absolute minimum action time in seconds (cannot go below this even with equipment bonuses).
    /// </summary>
    public const double AbsoluteMinActionTime = 0.1;

    /// <summary>
    /// Action time reduction per equipment speed point (each point = 0.02s faster).
    /// A weapon with +5 speed reduces action time by 0.1s.
    /// </summary>
    public const double ActionTimeReductionPerSpeedPoint = 0.02;

    /// <summary>
    /// Optional override for action time (used for stage enemies with stage-based speed tiers).
    /// When set, bypasses the normal speed-stat calculation entirely.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double? ActionTimeOverride { get; set; }
    
    /// <summary>
    /// Time reduction per speed upgrade in seconds.
    /// At 41 upgrades: 41 × (4.0/41) = 4.0s reduction, reaching 1.0s minimum.
    /// </summary>
    public const double ActionTimeReductionPerUpgrade = 4.0 / 41.0; // ~0.0976s per upgrade

    /// <summary>
    /// Calculates the action time in seconds.
    /// Based purely on SpeedUpgrades: from 5.0s (0 upgrades) to 1.0s (41 upgrades).
    /// Minimum is 1 second.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double ActionTime
    {
        get
        {
            // Stage enemies and penalty-buffed characters use a direct override.
            // Penalty buff allows going below MinActionTime (down to PenaltyMinActionTime = 0.5s),
            // so use the lower floor when an override is set.
            if (ActionTimeOverride.HasValue)
                return Math.Max(PenaltyMinActionTime, ActionTimeOverride.Value);

            // Speed upgrades provide the flat reduction: 5.0s → 1.0s over 41 upgrades
            var time = BaseActionTime - SpeedUpgrades * ActionTimeReductionPerUpgrade;

            // Equipment speed bonus provides additional action time reduction beyond the upgrade cap
            time -= EquipmentSpeedBonus * ActionTimeReductionPerSpeedPoint;

            return Math.Max(AbsoluteMinActionTime, time);
        }
    }

    /// <summary>
    /// Helper method to log speed changes for a character (for debugging/verification)
    /// </summary>
    public string GetSpeedChangeLog(string userName)
    {
        var currentActionTime = ActionTime;
        // Old formula would have been: 5.0 - (bonusSpeed * 0.02) - (SpeedUpgrades * 0.1)
        // For reference, if character was at 1.0s in the old formula, that meant either:
        // - SpeedUpgrades hit the old cap, or
        // - Character level was high enough to reach minimum
        return $"Character '{userName}' (SpeedUpgrades={SpeedUpgrades}): ActionTime = {currentActionTime:F1}s";
    }

    /// <summary>Gets the purchased bonus level for a specific equipment slot.</summary>
    public int GetSlotBonusLevel(Enums.EquipmentSlot slot) => slot switch
    {
        Enums.EquipmentSlot.Head => EquippedHeadBonusLevel,
        Enums.EquipmentSlot.Shoulders => EquippedShouldersBonusLevel,
        Enums.EquipmentSlot.Chest => EquippedChestBonusLevel,
        Enums.EquipmentSlot.Gloves => EquippedGlovesBonusLevel,
        Enums.EquipmentSlot.Legs => EquippedLegsBonusLevel,
        Enums.EquipmentSlot.Boots => EquippedBootsBonusLevel,
        _ => 0
    };

    /// <summary>Sets the purchased bonus level for a specific equipment slot.</summary>
    public void SetSlotBonusLevel(Enums.EquipmentSlot slot, int level)
    {
        switch (slot)
        {
            case Enums.EquipmentSlot.Head: EquippedHeadBonusLevel = level; break;
            case Enums.EquipmentSlot.Shoulders: EquippedShouldersBonusLevel = level; break;
            case Enums.EquipmentSlot.Chest: EquippedChestBonusLevel = level; break;
            case Enums.EquipmentSlot.Gloves: EquippedGlovesBonusLevel = level; break;
            case Enums.EquipmentSlot.Legs: EquippedLegsBonusLevel = level; break;
            case Enums.EquipmentSlot.Boots: EquippedBootsBonusLevel = level; break;
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
            HpUpgrades = 0,
            PowerUpgrades = 0,
            SpeedUpgrades = 0,
            CriticalUpgrades = 0,
            DefenseUpgrades = 0,
            // No equipment equipped by default
            EquippedHead = null,
            EquippedShoulders = null,
            EquippedChest = null,
            EquippedGloves = null,
            EquippedLegs = null,
            EquippedBoots = null,
            EquippedHeadQuality = 0,
            EquippedShouldersQuality = 0,
            EquippedChestQuality = 0,
            EquippedGlovesQuality = 0,
            EquippedLegsQuality = 0,
            EquippedBootsQuality = 0,
            EquippedHeadBonusLevel = 0,
            EquippedShouldersBonusLevel = 0,
            EquippedChestBonusLevel = 0,
            EquippedGlovesBonusLevel = 0,
            EquippedLegsBonusLevel = 0,
            EquippedBootsBonusLevel = 0,
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
    public static Character CreateStageEnemy(long hp, long power, long speed, long defense, double criticalChance, string enemyName, double? actionTimeOverride = null)
    {
        var character = new Character
        {
            UserId = "stage-enemy",
            Level = 1,
            XP = 0,
            HP = (int)Math.Min(hp, int.MaxValue),
            Power = (int)Math.Min(power, int.MaxValue),
            Speed = (int)Math.Min(speed, int.MaxValue),
            Defense = (int)Math.Min(defense, int.MaxValue),
            CriticalChance = criticalChance,
            CurrentHP = hp,
            HpUpgrades = 0,
            PowerUpgrades = 0,
            SpeedUpgrades = 0,
            CriticalUpgrades = 0,
            DefenseUpgrades = 0,
            ActionTimeOverride = actionTimeOverride
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
            EquippedHeadQuality = source.EquippedHeadQuality,
            EquippedShouldersQuality = source.EquippedShouldersQuality,
            EquippedChestQuality = source.EquippedChestQuality,
            EquippedGlovesQuality = source.EquippedGlovesQuality,
            EquippedLegsQuality = source.EquippedLegsQuality,
            EquippedBootsQuality = source.EquippedBootsQuality,
            EquippedHeadBonusLevel = source.EquippedHeadBonusLevel,
            EquippedShouldersBonusLevel = source.EquippedShouldersBonusLevel,
            EquippedChestBonusLevel = source.EquippedChestBonusLevel,
            EquippedGlovesBonusLevel = source.EquippedGlovesBonusLevel,
            EquippedLegsBonusLevel = source.EquippedLegsBonusLevel,
            EquippedBootsBonusLevel = source.EquippedBootsBonusLevel,
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

        var buffMultiplier = source.EffectiveShotBuffMultiplier;

        // Scale base stats AND upgrades — the compounding with the exponential
        // upgrade formula (stat × scale × (1+mult)^upgrades) is intentional:
        // investing in both Shot-buff upgrades and HP upgrades yields increasing returns.
        // The green-HP-bar overflow is prevented separately by clamping CurrentHP
        // to TotalHP in CombatActionService.CreateSession and JS updatePlayerHPBar.
        var buffedHP = (int)Math.Round(source.HP * buffMultiplier);
        var buffedHpUpgrades = (int)Math.Round(source.HpUpgrades * buffMultiplier);

        return new Character
        {
            Id = source.Id,
            UserId = source.UserId,
            Level = source.Level,
            XP = source.XP,
            // Boost base stats by buff multiplier
            HP = buffedHP,
            Power = (int)Math.Round(source.Power * buffMultiplier),
            Speed = (int)Math.Round(source.Speed * buffMultiplier),
            Defense = (int)Math.Round(source.Defense * buffMultiplier),
            CriticalChance = Math.Min(1.0, source.CriticalChance * buffMultiplier),
            // Scale HP upgrades — synergizes with the compound upgrade formula
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
            EquippedHeadQuality = source.EquippedHeadQuality,
            EquippedShouldersQuality = source.EquippedShouldersQuality,
            EquippedChestQuality = source.EquippedChestQuality,
            EquippedGlovesQuality = source.EquippedGlovesQuality,
            EquippedLegsQuality = source.EquippedLegsQuality,
            EquippedBootsQuality = source.EquippedBootsQuality,
            EquippedHeadBonusLevel = source.EquippedHeadBonusLevel,
            EquippedShouldersBonusLevel = source.EquippedShouldersBonusLevel,
            EquippedChestBonusLevel = source.EquippedChestBonusLevel,
            EquippedGlovesBonusLevel = source.EquippedGlovesBonusLevel,
            EquippedLegsBonusLevel = source.EquippedLegsBonusLevel,
            EquippedBootsBonusLevel = source.EquippedBootsBonusLevel,
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
            ShotBuffBattlesRemaining = source.ShotBuffBattlesRemaining,
            CigarroShieldHitsRemaining = source.CigarroShieldHitsRemaining,
            CanhaoDamageBoostHitsRemaining = source.CanhaoDamageBoostHitsRemaining,
            PenaltyBuffActive = source.PenaltyBuffActive
        };
    }

    /// <summary>
    /// Penalty buff speed reduction in seconds (subtracted from current action time).
    /// </summary>
    public const double PenaltySpeedReduction = 0.5;

    /// <summary>
    /// Penalty buff critical chance bonus (additive).
    /// A player with 50% crit becomes 100%, a player with 1% crit becomes 51%.
    /// </summary>
    public const double PenaltyCritBonus = 0.5;

    /// <summary>
    /// Minimum action time when penalty buff is active (allows going below normal MinActionTime).
    /// </summary>
    public const double PenaltyMinActionTime = 0.5;

    /// <summary>
    /// Creates a copy of the character with Penalty buff applied.
    /// Reduces action time by 0.5s (min 0.5s) and adds +50% crit chance (capped at 100%).
    /// </summary>
    public static Character CreatePenaltyBuffedCopy(Character source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        // Additive crit: +50% (capped at 1.0)
        var penaltyCrit = Math.Min(1.0, source.TotalCriticalChance + PenaltyCritBonus);
        // Speed reduction: -0.5s from current action time (min 0.5s)
        var penaltyActionTime = Math.Max(PenaltyMinActionTime, source.ActionTime - PenaltySpeedReduction);

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
            CriticalChance = penaltyCrit,
            HpUpgrades = source.HpUpgrades,
            PowerUpgrades = source.PowerUpgrades,
            SpeedUpgrades = source.SpeedUpgrades,
            // Upgrades already baked into penaltyCrit — zero out to prevent double-counting
            CriticalUpgrades = 0,
            DefenseUpgrades = source.DefenseUpgrades,
            EquippedHead = source.EquippedHead,
            EquippedShoulders = source.EquippedShoulders,
            EquippedChest = source.EquippedChest,
            EquippedGloves = source.EquippedGloves,
            EquippedLegs = source.EquippedLegs,
            EquippedBoots = source.EquippedBoots,
            EquippedHeadQuality = source.EquippedHeadQuality,
            EquippedShouldersQuality = source.EquippedShouldersQuality,
            EquippedChestQuality = source.EquippedChestQuality,
            EquippedGlovesQuality = source.EquippedGlovesQuality,
            EquippedLegsQuality = source.EquippedLegsQuality,
            EquippedBootsQuality = source.EquippedBootsQuality,
            EquippedHeadBonusLevel = source.EquippedHeadBonusLevel,
            EquippedShouldersBonusLevel = source.EquippedShouldersBonusLevel,
            EquippedChestBonusLevel = source.EquippedChestBonusLevel,
            EquippedGlovesBonusLevel = source.EquippedGlovesBonusLevel,
            EquippedLegsBonusLevel = source.EquippedLegsBonusLevel,
            EquippedBootsBonusLevel = source.EquippedBootsBonusLevel,
            EquippedWeapon1 = source.EquippedWeapon1,
            EquippedWeapon2 = source.EquippedWeapon2,
            EquipmentHPBonus = source.EquipmentHPBonus,
            EquipmentPowerBonus = source.EquipmentPowerBonus,
            EquipmentSpeedBonus = source.EquipmentSpeedBonus,
            EquipmentDefenseBonus = source.EquipmentDefenseBonus,
            EquipmentCriticalBonus = source.EquipmentCriticalBonus,
            ActionTimeOverride = penaltyActionTime,
            CurrentHP = source.CurrentHP,
            User = source.User,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            ShotBuffBattlesRemaining = source.ShotBuffBattlesRemaining,
            CigarroShieldHitsRemaining = source.CigarroShieldHitsRemaining,
            CanhaoDamageBoostHitsRemaining = source.CanhaoDamageBoostHitsRemaining,
            PenaltyBuffActive = source.PenaltyBuffActive
        };
    }

    /// <summary>
    /// Calculates XP required to advance from a given level to the next.
    /// Uses exponential formula: XpPerLevelBase × Level^XpGrowthExponent.
    /// Early levels are fast, late levels take days.
    /// </summary>
    public static int XpForLevel(int level) =>
        (int)Math.Round(MyTunoScaling.XpPerLevelBase * Math.Pow(level, MyTunoScaling.XpGrowthExponent));

    /// <summary>
    /// Adds XP to the character and handles level-ups.
    /// Uses exponential XP curve: each level requires XpPerLevelBase × Level^XpGrowthExponent XP.
    /// Level 1→2: 50 XP, Level 10→11: ~7,900 XP, Level 99→100: ~1,094,000 XP.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("XP amount cannot be negative", nameof(amount));

        XP += amount;

        // Level up logic: Each level requires XpForLevel(Level) XP
        // Max level cap prevents infinite leveling
        while (Level < MyTunoScaling.MaxLevel && XP >= XpForLevel(Level))
        {
            XP -= XpForLevel(Level);
            Level++;
            // Heal to full HP on level-up (accounts for shot buff)
            var maxHP = ShotBuffBattlesRemaining > 0
                ? CreateShotBuffedCopy(this).TotalHP
                : TotalHP;
            CurrentHP = maxHP;
        }
    }

    /// <summary>
    /// Increases the character's level by 1 (owner/admin operation).
    /// Resets XP to 0 and heals to full HP. Respects MaxLevel cap.
    /// </summary>
    public bool LevelUp()
    {
        if (Level >= MyTunoScaling.MaxLevel)
            return false;

        Level++;
        XP = 0;
        CurrentHP = null; // full HP
        return true;
    }

    /// <summary>
    /// Decreases the character's level by 1 (owner/admin operation).
    /// Resets XP to 0 and heals to full HP. Cannot go below level 1.
    /// </summary>
    public bool LevelDown()
    {
        if (Level <= 1)
            return false;

        Level--;
        XP = 0;
        CurrentHP = null; // full HP
        return true;
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

    // ── Improvements upgrade methods ──

    /// <summary>
    /// Upgrades energy capacity (increases MaxEnergy by configured amount per level)
    /// </summary>
    public void UpgradeEnergyAmount()
    {
        EnergyAmountUpgrades++;
        MaxEnergy = MyTunoScaling.BaseMaxEnergy + (int)(EnergyAmountUpgrades * MyTunoScaling.EnergyAmountPerUpgrade);
    }

    /// <summary>
    /// Upgrades energy regeneration speed
    /// </summary>
    public void UpgradeEnergyRegen()
    {
        EnergyRegenUpgrades++;
    }

    /// <summary>
    /// Upgrades the stat bonus given by the Shot buff
    /// </summary>
    public void UpgradeShotBuff()
    {
        ShotBuffUpgrades++;
    }

    /// <summary>
    /// Upgrades fidelis earned bonus
    /// </summary>
    public void UpgradeFidelisEarned()
    {
        FidelisEarnedUpgrades++;
    }

    // ── Powers upgrade methods ──

    /// <summary>
    /// Upgrades heavy attack damage multiplier
    /// </summary>
    public void UpgradeHeavyAttack()
    {
        HeavyAttackUpgrades++;
    }

    /// <summary>
    /// Upgrades special attack damage multiplier
    /// </summary>
    public void UpgradeSpecialAttack()
    {
        SpecialAttackUpgrades++;
    }

    /// <summary>
    /// Takes damage and updates CurrentHP
    /// </summary>
    public void TakeDamage(long damage)
    {
        var currentHp = CurrentHP ?? TotalHP;
        currentHp -= damage;
        CurrentHP = Math.Max(MinHP, currentHp);
    }

    /// <summary>
    /// Heals the character and updates CurrentHP
    /// Accounts for shot buff if active
    /// </summary>
    public void Heal(long amount)
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
    /// Expires the penalty buff (sets PenaltyBuffActive to 0).
    /// No HP scaling needed — penalty only affects speed and crit.
    /// </summary>
    public void ExpirePenaltyBuff()
    {
        PenaltyBuffActive = 0;
    }

    /// <summary>
    /// Decrements the shot buff counter by 1.
    /// When the buff fully expires (reaches 0), scales CurrentHP proportionally
    /// from the buffed max HP down to the unbuffed max HP so the HP bar stays consistent.
    /// </summary>
    public void ExpireShotBuff()
    {
        if (ShotBuffBattlesRemaining <= 0) return;

        // Capture buffed max HP before decrementing
        var buffedMaxHp = CreateShotBuffedCopy(this).TotalHP;

        ShotBuffBattlesRemaining--;

        if (ShotBuffBattlesRemaining == 0)
        {
            // Buff fully expired — scale CurrentHP proportionally back to unbuffed max
            var unbuffedMaxHp = TotalHP;
            var currentHp = CurrentHP ?? buffedMaxHp;
            var hpRatio = (double)currentHp / buffedMaxHp;
            CurrentHP = Math.Max(1, (long)Math.Round(hpRatio * unbuffedMaxHp));
        }
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
    /// Gets the display max HP for UI, accounting for shot buff.
    /// Uses CreateShotBuffedCopy to get the exact same value used in combat.
    /// </summary>
    public long GetDisplayMaxHP()
    {
        if (ShotBuffBattlesRemaining > 0)
        {
            var buffedCopy = CreateShotBuffedCopy(this);
            return buffedCopy.TotalHP;
        }
        return TotalHP;
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
