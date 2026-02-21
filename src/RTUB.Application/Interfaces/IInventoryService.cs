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
    /// Uses a Fino to heal the user's character (25% HP)
    /// </summary>
    Task<(bool Success, int HealedAmount, string Message)> UseFinoAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses a Caneca to heal the user's character (50% HP)
    /// </summary>
    Task<(bool Success, int HealedAmount, string Message)> UseCanecaAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses a Cigarro — shields the next 3 incoming hits (no damage taken)
    /// </summary>
    Task<(bool Success, string Message)> UseCigarroAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses a Canhão — next 3 outgoing hits deal 30% more damage
    /// </summary>
    Task<(bool Success, string Message)> UseCanhaoAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses a Penalty — 0.5s attack speed + 100% crit for 1 run/battle
    /// </summary>
    Task<(bool Success, string Message)> UsePenaltyAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of Fino in the user's inventory
    /// </summary>
    Task<int> GetFinoQuantityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of Caneca in the user's inventory
    /// </summary>
    Task<int> GetCanecaQuantityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of Cigarro in the user's inventory
    /// </summary>
    Task<int> GetCigarroQuantityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of Canhão in the user's inventory
    /// </summary>
    Task<int> GetCanhaoQuantityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of Penalty in the user's inventory
    /// </summary>
    Task<int> GetPenaltyQuantityAsync(string userId, CancellationToken cancellationToken = default);

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
    /// Gets all inventory item quantities for a user in a single query.
    /// Returns a dictionary of item type to quantity, including consumables, resources, parts, and equipment.
    /// </summary>
    Task<Dictionary<InventoryItemType, int>> GetUserInventorySummaryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current energy for an already-loaded character, applying passive regen since last check.
    /// Avoids a redundant character load when the caller already has the character.
    /// </summary>
    Task<(int CurrentEnergy, int MaxEnergy, int SecondsUntilNextRegen)> GetCurrentEnergyForCharacterAsync(Character character, CancellationToken cancellationToken = default);

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
    /// Gets all rare set item quantities for a user
    /// </summary>
    Task<Dictionary<InventoryItemType, int>> GetRareSetItemsAsync(string userId, CancellationToken cancellationToken = default);

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
    /// Discards ALL discardable items (equipment, instrument parts, and unequipped weapons) in one batch.
    /// Returns the total Fidelis gained and count of items discarded.
    /// </summary>
    Task<(bool Success, decimal TotalFidelis, int ItemsDiscarded, string Message)> DiscardAllItemsAsync(string userId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Upgrades a forged weapon by one level, increasing its stats. Costs Fidelis.
    /// </summary>
    Task<(bool Success, string Message)> UpgradeWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Fidelis cost to upgrade a weapon to the next level.
    /// </summary>
    decimal GetWeaponUpgradeCost(int currentLevel);

    /// <summary>
    /// Calculates which drink type and quantity are needed for an upgrade at a given level.
    /// Every 3 levels advances to the next drink tier (1→2→3, then next drink 1→2→3...).
    /// Used for WEAPON upgrades (single drink per tier).
    /// </summary>
    (InventoryItemType DrinkType, int Quantity) GetUpgradeDrinkRequirement(int currentLevel);

    /// <summary>
    /// Calculates ALL drink requirements for an EQUIPMENT upgrade at a given level.
    /// Equipment requires ALL drink tiers from tier 0 through the current tier (cumulative).
    /// </summary>
    List<(InventoryItemType DrinkType, int Quantity)> GetEquipmentUpgradeDrinkRequirements(int currentLevel);

    /// <summary>
    /// Gets the enhancement level for a specific equipment slot: floor(highestStage / 100) + slot purchased bonus.
    /// </summary>
    Task<int> GetSlotEnhancementLevelAsync(string userId, EquipmentSlot slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Fidelis cost to upgrade an equipment slot to the next level.
    /// </summary>
    decimal GetEquipmentUpgradeCost(int currentBonusLevel);

    /// <summary>
    /// Upgrades a specific equipment slot's enhancement level by 1. Costs Fidelis + drinks.
    /// </summary>
    Task<(bool Success, string Message)> UpgradeEquipmentSlotAsync(string userId, EquipmentSlot slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the Fidelis value for discarding a forged weapon.
    /// </summary>
    decimal GetWeaponDiscardValue(ForgedWeapon weapon, int characterLevel);

    /// <summary>
    /// Discards a forged weapon in exchange for Fidelis currency.
    /// Unequips the weapon first if it is currently equipped.
    /// </summary>
    Task<(bool Success, decimal FidelisGained, string Message)> DiscardWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a rare set piece from inventory as a permanent upgrade to the matching equipment slot.
    /// Consumes 1 item from inventory and sets the corresponding RareXxxApplied flag on the character.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="rareItemType">The rare set piece InventoryItemType (300–305)</param>
    /// <returns>Success flag and message</returns>
    Task<(bool Success, string Message)> ApplyRareSetUpgradeAsync(string userId, InventoryItemType rareItemType, CancellationToken cancellationToken = default);
}
