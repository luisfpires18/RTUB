using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// A weapon forged by combining an instrument part with a drink at the forge.
/// Each forged weapon has a unique name chosen by the player, a type, and stat bonuses.
/// </summary>
public class ForgedWeapon : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Player-chosen name for the weapon (e.g. "Guitarra do Poder da Vodka")</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The type of weapon (Sword, Axe, Staff, etc.)</summary>
    public WeaponType WeaponType { get; set; }

    /// <summary>Whether this weapon requires both weapon slots</summary>
    public bool IsTwoHanded { get; set; }

    /// <summary>The instrument part type used to forge this weapon</summary>
    public InventoryItemType SourceInstrument { get; set; }

    /// <summary>The drink type used to forge this weapon</summary>
    public InventoryItemType SourceDrink { get; set; }

    // Stat bonuses
    public int BonusHP { get; set; }
    public int BonusPower { get; set; }
    public int BonusSpeed { get; set; }
    public int BonusDefense { get; set; }
    public double BonusCriticalChance { get; set; }

    /// <summary>Whether this weapon is currently equipped</summary>
    public bool IsEquipped { get; set; }

    /// <summary>Weapon enhancement level (+1, +2, +3, etc.)</summary>
    public int Level { get; set; }

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private ForgedWeapon() { }

    /// <summary>
    /// Factory method to create a new forged weapon
    /// </summary>
    public static ForgedWeapon Create(
        string userId,
        string name,
        WeaponType weaponType,
        InventoryItemType sourceInstrument,
        InventoryItemType sourceDrink,
        int bonusHP = 0,
        int bonusPower = 0,
        int bonusSpeed = 0,
        int bonusDefense = 0,
        double bonusCriticalChance = 0)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Weapon name is required", nameof(name));

        var isTwoHanded = weaponType == WeaponType.Staff || weaponType == WeaponType.Bow;

        return new ForgedWeapon
        {
            UserId = userId,
            Name = name,
            WeaponType = weaponType,
            IsTwoHanded = isTwoHanded,
            SourceInstrument = sourceInstrument,
            SourceDrink = sourceDrink,
            BonusHP = bonusHP,
            BonusPower = bonusPower,
            BonusSpeed = bonusSpeed,
            BonusDefense = bonusDefense,
            BonusCriticalChance = bonusCriticalChance,
            IsEquipped = false
        };
    }
}
