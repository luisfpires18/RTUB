namespace RTUB.Core.Enums;

/// <summary>
/// Represents the type of inventory item
/// </summary>
public enum InventoryItemType
{
    /// <summary>
    /// Beer item - used for healing characters
    /// </summary>
    Beer = 1,

    /// <summary>
    /// Shot item - dropped by stage enemies, used for special abilities
    /// </summary>
    Shot = 2,

    /// <summary>
    /// Vodka - gathered resource, used for crafting (costs 1 energy)
    /// </summary>
    Vodka = 3,

    /// <summary>
    /// Gin - gathered resource, used for crafting (costs 2 energy)
    /// </summary>
    Gin = 4,

    /// <summary>
    /// Whisky - gathered resource, used for crafting (costs 3 energy)
    /// </summary>
    Whisky = 5,

    /// <summary>
    /// Absinto - gathered resource, used for crafting (costs 4 energy)
    /// </summary>
    Absinto = 6
}
