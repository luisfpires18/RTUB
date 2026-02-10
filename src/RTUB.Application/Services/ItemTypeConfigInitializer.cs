using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Initializes default ItemTypeConfig seed data on application startup.
/// Ensures all weapon types, drink types, and equipment types have config rows in the database.
/// </summary>
public class ItemTypeConfigInitializer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ItemTypeConfigInitializer> _logger;

    private static readonly InventoryItemType[] DrinkTypes =
    [
        InventoryItemType.Cerveja, InventoryItemType.Vinho, InventoryItemType.Licor,
        InventoryItemType.Rum, InventoryItemType.Tequilla, InventoryItemType.Vodka,
        InventoryItemType.Gin, InventoryItemType.Whisky, InventoryItemType.Absinto,
        InventoryItemType.Aguardente
    ];

    private static readonly (InventoryItemType Type, string Label, string Icon)[] ConsumableTypes =
    [
        (InventoryItemType.Fino, "Fino", "bi-cup-straw"),
        (InventoryItemType.Caneca, "Caneca", "bi-cup-hot"),
        (InventoryItemType.Cigarro, "Cigarro", "bi-cloud"),
        (InventoryItemType.Canhao, "Canhão", "bi-bullseye"),
        (InventoryItemType.Shot, "Shot", "bi-droplet-fill")
    ];

    private static readonly (InventoryItemType Type, string Label, string Icon)[] EquipmentSlots =
    [
        (InventoryItemType.EquipmentHead, "Cabeça", "bi-cap-front"),
        (InventoryItemType.EquipmentShoulders, "Ombros", "bi-chevron-bar-expand"),
        (InventoryItemType.EquipmentChest, "Peito", "bi-shield-fill"),
        (InventoryItemType.EquipmentGloves, "Luvas", "bi-hand-index-fill"),
        (InventoryItemType.EquipmentLegs, "Pernas", "bi-signpost-split-fill"),
        (InventoryItemType.EquipmentBoots, "Botas", "bi-box-arrow-in-down")
    ];

    public ItemTypeConfigInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<ItemTypeConfigInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Seeds any missing item type configs into the database.
    /// Safe to call multiple times — only inserts new records.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IItemTypeConfigRepository>();

        var existingConfigs = await repository.GetAllOrderedAsync();
        var existingKeys = existingConfigs.Select(c => c.TypeKey).ToHashSet();
        var added = 0;

        // Seed weapons
        foreach (var wt in Enum.GetValues<WeaponType>())
        {
            var typeKey = wt.ToString();
            if (!existingKeys.Contains(typeKey))
            {
                var config = ItemTypeConfig.Create(
                    typeKey,
                    WeaponTypeHelper.GetDisplayName(wt),
                    WeaponTypeHelper.GetIcon(wt),
                    "Weapon");
                await repository.AddAsync(config);
                added++;
            }
        }

        // Seed drinks
        foreach (var dt in DrinkTypes)
        {
            var typeKey = dt.ToString();
            if (!existingKeys.Contains(typeKey))
            {
                var config = ItemTypeConfig.Create(
                    typeKey,
                    GetDrinkDisplayName(dt),
                    "bi-cup-straw",
                    "Drink");
                await repository.AddAsync(config);
                added++;
            }
        }

        // Seed equipment
        foreach (var (eqType, label, icon) in EquipmentSlots)
        {
            var typeKey = eqType.ToString();
            if (!existingKeys.Contains(typeKey))
            {
                var config = ItemTypeConfig.Create(
                    typeKey,
                    label,
                    icon,
                    "Equipment");
                await repository.AddAsync(config);
                added++;
            }
        }

        // Seed consumables
        foreach (var (conType, label, icon) in ConsumableTypes)
        {
            var typeKey = conType.ToString();
            if (!existingKeys.Contains(typeKey))
            {
                var config = ItemTypeConfig.Create(
                    typeKey,
                    label,
                    icon,
                    "Consumable");
                await repository.AddAsync(config);
                added++;
            }
        }

        if (added > 0)
        {
            _logger.LogInformation("Seeded {Count} item type configs", added);
        }
    }

    private static string GetDrinkDisplayName(InventoryItemType type) => type switch
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
        _ => type.ToString()
    };
}
