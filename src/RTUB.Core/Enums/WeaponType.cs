namespace RTUB.Core.Enums;

/// <summary>
/// Types of forged weapons that can be created from instruments + drinks.
/// </summary>
public enum WeaponType
{
    // One-handed weapons
    /// <summary>One-handed sword</summary>
    SwordOneHand = 1,
    /// <summary>One-handed axe</summary>
    AxeOneHand = 2,
    /// <summary>One-handed mace</summary>
    Mace = 3,
    /// <summary>One-handed shield (off-hand)</summary>
    Shield = 5,
    /// <summary>One-handed dagger</summary>
    Dagger = 11,

    // Two-handed weapons
    /// <summary>Two-handed staff</summary>
    Staff = 4,
    /// <summary>Two-handed bow</summary>
    Bow = 6,
    /// <summary>Two-handed sword</summary>
    SwordTwoHand = 7,
    /// <summary>Two-handed spear</summary>
    Spear = 8,
    /// <summary>Two-handed axe</summary>
    AxeTwoHand = 9,
    /// <summary>Two-handed hammer</summary>
    Hammer = 10
}
