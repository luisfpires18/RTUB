using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for WeaponType display names and properties.
/// </summary>
public static class WeaponTypeHelper
{
    /// <summary>
    /// Gets a localized display name for a weapon type.
    /// </summary>
    public static string GetDisplayName(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Sword => "Espada",
            WeaponType.Axe => "Machado",
            WeaponType.Mace => "Maça",
            WeaponType.Staff => "Cajado",
            WeaponType.Shield => "Escudo",
            WeaponType.Bow => "Arco",
            WeaponType.Dagger => "Adaga",
            _ => weaponType.ToString()
        };
    }

    /// <summary>
    /// Gets a Bootstrap icon for a weapon type.
    /// </summary>
    public static string GetIcon(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Sword => "bi-slash-lg",
            WeaponType.Axe => "bi-hammer",
            WeaponType.Mace => "bi-hammer",
            WeaponType.Staff => "bi-magic",
            WeaponType.Shield => "bi-shield-fill",
            WeaponType.Bow => "bi-bullseye",
            WeaponType.Dagger => "bi-lightning",
            _ => "bi-sword"
        };
    }

    /// <summary>
    /// Returns true if the weapon type requires both weapon slots.
    /// </summary>
    public static bool IsTwoHanded(WeaponType weaponType)
    {
        return weaponType == WeaponType.Staff || weaponType == WeaponType.Bow;
    }

    /// <summary>
    /// Gets all weapon types.
    /// </summary>
    public static IReadOnlyList<WeaponType> AllWeaponTypes { get; } =
        Enum.GetValues(typeof(WeaponType))
            .Cast<WeaponType>()
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Generates a default weapon name based on instrument and drink used.
    /// </summary>
    public static string GenerateDefaultName(WeaponType weaponType, InventoryItemType sourceInstrument, InventoryItemType sourceDrink)
    {
        var weaponName = GetDisplayName(weaponType);
        var instrName = InstrumentTypeHelper.FromInventoryPartType(sourceInstrument) is { } instrType
            ? InstrumentTypeHelper.GetDisplayName(instrType)
            : "Instrumento";
        var drinkName = sourceDrink switch
        {
            InventoryItemType.Vodka => "Vodka",
            InventoryItemType.Gin => "Gin",
            InventoryItemType.Whisky => "Whisky",
            InventoryItemType.Absinto => "Absinto",
            _ => "Bebida"
        };

        return $"{weaponName} da {instrName} com {drinkName}";
    }
}
