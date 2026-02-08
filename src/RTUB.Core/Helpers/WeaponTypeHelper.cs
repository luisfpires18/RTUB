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
            WeaponType.SwordOneHand => "Sword (1H)",
            WeaponType.AxeOneHand => "Axe (1H)",
            WeaponType.Mace => "Mace (1H)",
            WeaponType.Shield => "Shield (1H)",
            WeaponType.Dagger => "Dagger (1H)",
            WeaponType.Staff => "Staff (2H)",
            WeaponType.Bow => "Bow (2H)",
            WeaponType.SwordTwoHand => "Sword (2H)",
            WeaponType.Spear => "Spear (2H)",
            WeaponType.AxeTwoHand => "Axe (2H)",
            WeaponType.Hammer => "Hammer (2H)",
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
            WeaponType.SwordOneHand => "bi-slash-lg",
            WeaponType.SwordTwoHand => "bi-slash-lg",
            WeaponType.AxeOneHand => "bi-hammer",
            WeaponType.AxeTwoHand => "bi-hammer",
            WeaponType.Mace => "bi-hammer",
            WeaponType.Staff => "bi-magic",
            WeaponType.Shield => "bi-shield-fill",
            WeaponType.Dagger => "bi-lightning",
            WeaponType.Bow => "bi-bullseye",
            WeaponType.Spear => "bi-chevron-bar-up",
            WeaponType.Hammer => "bi-hammer",
            _ => "bi-sword"
        };
    }

    /// <summary>
    /// Returns true if the weapon type requires both weapon slots.
    /// </summary>
    public static bool IsTwoHanded(WeaponType weaponType)
    {
        return weaponType is WeaponType.Staff or WeaponType.Bow
            or WeaponType.SwordTwoHand or WeaponType.Spear
            or WeaponType.AxeTwoHand or WeaponType.Hammer;
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
            : "Instrument";
        var drinkName = sourceDrink switch
        {
            InventoryItemType.Cerveja => "Cerveja",
            InventoryItemType.Vinho => "Vinho",
            InventoryItemType.Licor => "Licor",
            InventoryItemType.Rum => "Rum",
            InventoryItemType.Tequilla => "Tequilla",
            InventoryItemType.Vodka => "Vodka",
            InventoryItemType.Gin => "Gin",
            InventoryItemType.Whisky => "Whisky",
            InventoryItemType.Absinto => "Absinto",
            InventoryItemType.Aguardente => "Aguardente",
            _ => "Drink"
        };

        return $"{weaponName} {instrName} + {drinkName}";
    }
}
