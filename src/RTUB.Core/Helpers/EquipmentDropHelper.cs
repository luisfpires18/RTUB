using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for equipment slot display names and inventory type mapping.
/// </summary>
public static class EquipmentDropHelper
{
    /// <summary>
    /// Gets a localized display name for an equipment slot.
    /// </summary>
    public static string GetDisplayName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => "HEAD",
            EquipmentSlot.Shoulders => "SHOULDERS",
            EquipmentSlot.Chest => "CHEST",
            EquipmentSlot.Gloves => "GLOVES",
            EquipmentSlot.Legs => "LEGS",
            EquipmentSlot.Boots => "BOOTS",
            _ => slot.ToString()
        };
    }

    /// <summary>
    /// Gets the default Bootstrap icon class for an equipment slot (fallback when no sprite configured).
    /// </summary>
    public static string GetFallbackIcon(EquipmentSlot slot)
    {
        return "bi-shield-shaded";
    }

    /// <summary>
    /// Converts an EquipmentSlot to its corresponding InventoryItemType.
    /// </summary>
    public static InventoryItemType ToInventoryItemType(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => InventoryItemType.EquipmentHead,
            EquipmentSlot.Shoulders => InventoryItemType.EquipmentShoulders,
            EquipmentSlot.Chest => InventoryItemType.EquipmentChest,
            EquipmentSlot.Gloves => InventoryItemType.EquipmentGloves,
            EquipmentSlot.Legs => InventoryItemType.EquipmentLegs,
            EquipmentSlot.Boots => InventoryItemType.EquipmentBoots,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown equipment slot")
        };
    }

    /// <summary>
    /// Converts an InventoryItemType back to its EquipmentSlot.
    /// Returns null if the item type is not an equipment piece.
    /// </summary>
    public static EquipmentSlot? FromInventoryItemType(InventoryItemType itemType)
    {
        return itemType switch
        {
            InventoryItemType.EquipmentHead => EquipmentSlot.Head,
            InventoryItemType.EquipmentShoulders => EquipmentSlot.Shoulders,
            InventoryItemType.EquipmentChest => EquipmentSlot.Chest,
            InventoryItemType.EquipmentGloves => EquipmentSlot.Gloves,
            InventoryItemType.EquipmentLegs => EquipmentSlot.Legs,
            InventoryItemType.EquipmentBoots => EquipmentSlot.Boots,
            _ => null
        };
    }

    /// <summary>
    /// Checks if an InventoryItemType is an equipment piece.
    /// </summary>
    public static bool IsEquipment(InventoryItemType itemType)
    {
        return (int)itemType >= 200 && (int)itemType <= 205;
    }

    /// <summary>
    /// Gets all equipment InventoryItemType values.
    /// </summary>
    public static IReadOnlyList<InventoryItemType> AllEquipmentTypes { get; } =
        Enum.GetValues(typeof(EquipmentSlot))
            .Cast<EquipmentSlot>()
            .Select(ToInventoryItemType)
            .ToList()
            .AsReadOnly();
}
