using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for inventory operations
/// Provides high-level business logic for inventory management
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Uses a beer to heal the user's character
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing success status, healed amount, and message</returns>
    Task<(bool Success, int HealedAmount, string Message)> UseBeerAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of beer in the user's inventory
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The quantity of beer, or 0 if none</returns>
    Task<int> GetBeerQuantityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of shots in the user's inventory
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The quantity of shots, or 0 if none</returns>
    Task<int> GetShotQuantityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses a shot to empower the character's next 5 arena battles
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing success status, battles empowered, and message</returns>
    Task<(bool Success, int BattlesEmpowered, string Message)> UseShotAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of a resource type in the user's inventory
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="type">The inventory item type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The quantity of the resource, or 0 if none</returns>
    Task<int> GetResourceQuantityAsync(string userId, InventoryItemType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current energy for a character, including passive regeneration
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of current energy, max energy, and seconds until next energy regen tick</returns>
    Task<(int CurrentEnergy, int MaxEnergy, int SecondsUntilNextRegen)> GetCurrentEnergyAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gathers a resource by spending energy
    /// Energy costs: Vodka=1, Gin=2, Whisky=3, Absinto=4
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="resourceType">The type of resource to gather</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing success status, gathered quantity, remaining energy, and message</returns>
    Task<(bool Success, int Gathered, int RemainingEnergy, string Message)> GatherResourceAsync(string userId, InventoryItemType resourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all instrument part quantities for a user
    /// </summary>
    Task<Dictionary<InventoryItemType, int>> GetInstrumentPartsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all equipment item quantities for a user
    /// </summary>
    Task<Dictionary<InventoryItemType, int>> GetEquipmentItemsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Equips an item from inventory to the character's equipment slot.
    /// Consumes 1 from inventory and sets the equipped slot.
    /// </summary>
    Task<(bool Success, string Message)> EquipItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unequips an item from a character's slot back to inventory.
    /// </summary>
    Task<(bool Success, string Message)> UnequipItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards an inventory item in exchange for Fidelis currency.
    /// Consumes 1 from inventory and credits the Fidelis value to the user.
    /// </summary>
    Task<(bool Success, decimal FidelisGained, string Message)> DiscardItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forges a weapon by consuming an instrument part and a drink.
    /// </summary>
    Task<(bool Success, ForgedWeapon? Weapon, string Message)> ForgeWeaponAsync(string userId, InventoryItemType instrumentPart, InventoryItemType drink, WeaponType weaponType, string weaponName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all forged weapons for a user.
    /// </summary>
    Task<List<ForgedWeapon>> GetForgedWeaponsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Equips a forged weapon to a weapon slot (1 or 2).
    /// </summary>
    Task<(bool Success, string Message)> EquipWeaponAsync(string userId, int weaponId, int slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unequips a weapon from a slot (1 or 2).
    /// </summary>
    Task<(bool Success, string Message)> UnequipWeaponAsync(string userId, int slot, CancellationToken cancellationToken = default);
}
