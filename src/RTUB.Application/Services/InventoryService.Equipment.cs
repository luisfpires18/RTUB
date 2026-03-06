using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Equipment — instrument parts, equipment slots, equip/unequip/discard items
public partial class InventoryService
{
    public async Task<Dictionary<InventoryItemType, int>> GetInstrumentPartsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allItems = await _inventoryRepository.GetUserInventoryAsync(userId, cancellationToken);
        return allItems
            .Where(i => InstrumentTypeHelper.IsInstrumentPart(i.Type) && i.Quantity > 0)
            .ToDictionary(i => i.Type, i => i.Quantity);
    }

    /// <summary>
    /// Gets all equipment item quantities for a user (items with type 200-205)
    /// </summary>
    public async Task<Dictionary<InventoryItemType, int>> GetEquipmentItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allItems = await _inventoryRepository.GetUserInventoryAsync(userId, cancellationToken);
        return allItems
            .Where(i => EquipmentDropHelper.IsEquipment(i.Type) && i.Quantity > 0)
            .ToDictionary(i => i.Type, i => i.Quantity);
    }

    public async Task<Dictionary<InventoryItemType, int>> GetRareSetItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allItems = await _inventoryRepository.GetUserInventoryAsync(userId, cancellationToken);
        return allItems
            .Where(i => EquipmentDropHelper.IsRareSetPiece(i.Type) && i.Quantity > 0)
            .ToDictionary(i => i.Type, i => i.Quantity);
    }

    /// <summary>
    /// Equips an item from inventory to the corresponding character slot.
    /// Consumes 1 from inventory, unequips the current piece (returning it to inventory) if any,
    /// sets the slot, and recalculates equipment stat bonuses.
    /// </summary>
    public async Task<(bool Success, string Message)> EquipItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default)
    {
        // Validate the item is equippable
        var isEquipment = EquipmentDropHelper.IsEquipment(itemType);
        if (!isEquipment)
            return (false, "Este item não pode ser equipado");

        // Check inventory
        var item = await _inventoryRepository.GetItemAsync(userId, itemType, cancellationToken);
        if (item == null || item.Quantity <= 0)
            return (false, "Não tens este item no inventário");

        // Get character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return (false, "Personagem não encontrado");

        // Determine slot and unequip current item if occupied
        var slot = EquipmentDropHelper.FromInventoryItemType(itemType);
        if (slot == null)
            return (false, "Slot de equipamento inválido");

        InventoryItemType? currentlyEquipped = slot.Value switch
        {
            EquipmentSlot.Head => character.EquippedHead,
            EquipmentSlot.Shoulders => character.EquippedShoulders,
            EquipmentSlot.Chest => character.EquippedChest,
            EquipmentSlot.Gloves => character.EquippedGloves,
            EquipmentSlot.Legs => character.EquippedLegs,
            EquipmentSlot.Boots => character.EquippedBoots,
            _ => null
        };

        // Set new item in slot + roll random quality
        var qualityMin = _scalingConfig.StageMode.EquipmentQualityMin;
        var qualityMax = _scalingConfig.StageMode.EquipmentQualityMax;
        var rng = new Random();
        var newQuality = qualityMin + rng.NextDouble() * (qualityMax - qualityMin);

        switch (slot.Value)
        {
            case EquipmentSlot.Head: character.EquippedHead = itemType; character.EquippedHeadQuality = newQuality; break;
            case EquipmentSlot.Shoulders: character.EquippedShoulders = itemType; character.EquippedShouldersQuality = newQuality; break;
            case EquipmentSlot.Chest: character.EquippedChest = itemType; character.EquippedChestQuality = newQuality; break;
            case EquipmentSlot.Gloves: character.EquippedGloves = itemType; character.EquippedGlovesQuality = newQuality; break;
            case EquipmentSlot.Legs: character.EquippedLegs = itemType; character.EquippedLegsQuality = newQuality; break;
            case EquipmentSlot.Boots: character.EquippedBoots = itemType; character.EquippedBootsQuality = newQuality; break;
        }

        // Return currently equipped item to inventory (only if it's a different type;
        // same type is fungible so re-equipping just re-rolls quality)
        if (currentlyEquipped.HasValue && currentlyEquipped.Value != itemType)
        {
            await _inventoryRepository.AddItemAsync(userId, currentlyEquipped.Value, 1, cancellationToken);
        }

        // Consume 1 from inventory
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
            return (false, "Erro ao consumir item do inventário");

        await RecalculateEquipmentBonusesAsync(character, cancellationToken);

        await _characterRepository.UpdateAsync(character);

        var displayName = EquipmentDropHelper.GetDisplayName(EquipmentDropHelper.FromInventoryItemType(itemType)!.Value);

        return (true, $"{displayName} equipado!");
    }

    /// <summary>
    /// Unequips an item from a character slot and returns it to inventory.
    /// </summary>
    public async Task<(bool Success, string Message)> UnequipItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default)
    {
        var isEquipment = EquipmentDropHelper.IsEquipment(itemType);
        if (!isEquipment)
            return (false, "Este item não pode ser desequipado");

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return (false, "Personagem não encontrado");

        // Verify item is actually equipped
        var slot = EquipmentDropHelper.FromInventoryItemType(itemType);
        if (slot == null)
            return (false, "Slot de equipamento inválido");

        InventoryItemType? currentlyEquipped = slot.Value switch
        {
            EquipmentSlot.Head => character.EquippedHead,
            EquipmentSlot.Shoulders => character.EquippedShoulders,
            EquipmentSlot.Chest => character.EquippedChest,
            EquipmentSlot.Gloves => character.EquippedGloves,
            EquipmentSlot.Legs => character.EquippedLegs,
            EquipmentSlot.Boots => character.EquippedBoots,
            _ => null
        };

        if (currentlyEquipped != itemType)
            return (false, "Este item não está equipado neste slot");

        // Clear slot + quality
        switch (slot.Value)
        {
            case EquipmentSlot.Head: character.EquippedHead = null; character.EquippedHeadQuality = 0; break;
            case EquipmentSlot.Shoulders: character.EquippedShoulders = null; character.EquippedShouldersQuality = 0; break;
            case EquipmentSlot.Chest: character.EquippedChest = null; character.EquippedChestQuality = 0; break;
            case EquipmentSlot.Gloves: character.EquippedGloves = null; character.EquippedGlovesQuality = 0; break;
            case EquipmentSlot.Legs: character.EquippedLegs = null; character.EquippedLegsQuality = 0; break;
            case EquipmentSlot.Boots: character.EquippedBoots = null; character.EquippedBootsQuality = 0; break;
        }

        // Return to inventory
        await _inventoryRepository.AddItemAsync(userId, itemType, 1, cancellationToken);

        await RecalculateEquipmentBonusesAsync(character, cancellationToken);

        await _characterRepository.UpdateAsync(character);

        var displayName = EquipmentDropHelper.GetDisplayName(EquipmentDropHelper.FromInventoryItemType(itemType)!.Value);

        return (true, $"{displayName} desequipado!");
    }

    /// <summary>
    /// Discards an inventory item in exchange for Fidelis currency.
    /// Consumes 1 from inventory and credits the Fidelis value to the user.
    /// </summary>
    public async Task<(bool Success, decimal FidelisGained, string Message)> DiscardItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default)
    {
        var isEquipment = EquipmentDropHelper.IsEquipment(itemType);
        var isInstrument = InstrumentTypeHelper.IsInstrumentPart(itemType);
        if (!isEquipment && !isInstrument)
            return (false, 0, "Este item não pode ser descartado");

        // Check inventory
        var item = await _inventoryRepository.GetItemAsync(userId, itemType, cancellationToken);
        if (item == null || item.Quantity <= 0)
            return (false, 0, "Não tens este item no inventário");

        // Determine Fidelis value (scales with player level and enhancement)
        var discardValues = _scalingConfig.StageMode.DiscardValues;
        var baseValue = isEquipment ? discardValues.Equipment : discardValues.InstrumentPart;

        // Apply level scaling to discard value — use fresh context per operation
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        var level = character?.Level ?? 1;
        var discardScale = 1.0 + level * _scalingConfig.StageMode.DiscardLevelScale;

        // Apply enhancement multiplier for equipment (per-slot level)
        var enhancementMult = 1.0;
        if (isEquipment && character != null)
        {
            var slot = EquipmentDropHelper.FromInventoryItemType(itemType);
            if (slot.HasValue)
            {
                var slotBonus = character.GetSlotBonusLevel(slot.Value);
                enhancementMult = 1.0 + slotBonus * _scalingConfig.StageMode.EquipmentEnhancementBonus;
            }
        }

        var fidelisValue = Math.Round(baseValue * (decimal)(discardScale * enhancementMult), 2);

        // Consume 1 from inventory
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
            return (false, 0, "Erro ao descartar item");

        // Credit Fidelis to user
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user != null)
        {
            user.FidelisBalance += fidelisValue;
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            await ctx.SaveChangesAsync(cancellationToken);
        }

        var displayName = isEquipment
            ? EquipmentDropHelper.GetDisplayName(EquipmentDropHelper.FromInventoryItemType(itemType)!.Value)
            : InstrumentTypeHelper.GetDisplayName(InstrumentTypeHelper.FromInventoryPartType(itemType)!.Value);

        return (true, fidelisValue, $"{displayName} descartado por {fidelisValue:F2} Fidelis!");
    }

    /// <summary>
    /// Discards ALL discardable items (equipment, instrument parts, and unequipped weapons) in one batch.
    /// Returns the total Fidelis gained and a summary of items discarded.
    /// </summary>
    public async Task<(bool Success, decimal TotalFidelis, int ItemsDiscarded, string Message)> DiscardAllItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        var charLevel = character?.Level ?? 1;
        var discardValues = _scalingConfig.StageMode.DiscardValues;
        var discardLevelScale = _scalingConfig.StageMode.DiscardLevelScale;
        var charLevelMult = 1.0 + charLevel * discardLevelScale;

        // Per-slot enhancement multipliers for equipment (manual upgrades only)
        double GetSlotEnhMult(InventoryItemType itemType)
        {
            var slot = EquipmentDropHelper.FromInventoryItemType(itemType);
            if (slot.HasValue && character != null)
            {
                var slotLevel = character.GetSlotBonusLevel(slot.Value);
                return 1.0 + slotLevel * _scalingConfig.StageMode.EquipmentEnhancementBonus;
            }
            return 1.0;
        }

        // Build set of currently equipped item types so we never discard gear that is worn
        var equippedTypes = new HashSet<InventoryItemType>();
        if (character != null)
        {
            if (character.EquippedHead.HasValue) equippedTypes.Add(character.EquippedHead.Value);
            if (character.EquippedShoulders.HasValue) equippedTypes.Add(character.EquippedShoulders.Value);
            if (character.EquippedChest.HasValue) equippedTypes.Add(character.EquippedChest.Value);
            if (character.EquippedGloves.HasValue) equippedTypes.Add(character.EquippedGloves.Value);
            if (character.EquippedLegs.HasValue) equippedTypes.Add(character.EquippedLegs.Value);
            if (character.EquippedBoots.HasValue) equippedTypes.Add(character.EquippedBoots.Value);
        }

        decimal totalFidelis = 0;
        int totalItems = 0;

        // 1. Discard all equipment / instrument items (skip currently equipped types)
        var equipmentInventory = await ctx.InventoryItems
            .Where(i => i.UserId == userId && i.Quantity > 0)
            .ToListAsync(cancellationToken);

        foreach (var item in equipmentInventory)
        {
            var isEquipment = EquipmentDropHelper.IsEquipment(item.Type);
            var isInstrument = InstrumentTypeHelper.IsInstrumentPart(item.Type);
            if (!isEquipment && !isInstrument) continue;

            // Never discard equipment that is currently equipped on the character
            if (isEquipment && equippedTypes.Contains(item.Type)) continue;

            var baseValue = isEquipment ? discardValues.Equipment : discardValues.InstrumentPart;
            var itemEnhMult = isEquipment ? GetSlotEnhMult(item.Type) : 1.0;
            var perUnitValue = Math.Round(baseValue * (decimal)(charLevelMult * itemEnhMult), 2);
            var quantity = item.Quantity;

            var consumed = await _inventoryRepository.ConsumeItemAsync(userId, item.Type, quantity, cancellationToken);
            if (consumed)
            {
                totalFidelis += perUnitValue * quantity;
                totalItems += quantity;
            }
        }

        // 2. Discard all unequipped weapons
        var unequippedWeapons = await ctx.ForgedWeapons
            .Where(w => w.UserId == userId && !w.IsEquipped)
            .ToListAsync(cancellationToken);

        foreach (var weapon in unequippedWeapons)
        {
            var weaponValue = GetWeaponDiscardValue(weapon, charLevel);
            ctx.ForgedWeapons.Remove(weapon);
            totalFidelis += weaponValue;
            totalItems++;
        }

        if (totalItems == 0)
            return (false, 0, 0, "Nenhum item para descartar");

        totalFidelis = Math.Round(totalFidelis, 2);

        // Credit Fidelis
        var user = await ctx.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null)
        {
            user.FidelisBalance += totalFidelis;
        }

        await ctx.SaveChangesAsync(cancellationToken);

        return (true, totalFidelis, totalItems, $"{totalItems} itens descartados por {totalFidelis:F2} Fidelis!");
    }

    // ── Forging ──

    private static readonly HashSet<InventoryItemType> DrinkTypes = new()
    {
        InventoryItemType.Cerveja, InventoryItemType.Vinho,
        InventoryItemType.Licor, InventoryItemType.Rum,
        InventoryItemType.Tequilla, InventoryItemType.Vodka,
        InventoryItemType.Gin, InventoryItemType.Whisky,
        InventoryItemType.Absinto, InventoryItemType.Aguardente
    };

}
