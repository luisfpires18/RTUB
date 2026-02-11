using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for inventory operations
/// Handles business logic for using and managing inventory items
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InventoryService> _logger;
    private readonly GatheringConfig _gatheringConfig;
    private readonly MyTunoScalingConfiguration _scalingConfig;
    private readonly ApplicationDbContext _dbContext;

    // Fino heals 25% of total HP
    private const double FinoHealPercentage = 0.25;
    // Caneca heals 50% of total HP
    private const double CanecaHealPercentage = 0.50;
    // Cigarro shields next 3 incoming hits
    private const int CigarroShieldHits = 3;
    // Canhão boosts next 3 outgoing hits by 30%
    private const int CanhaoDamageBoostHits = 3;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryService> logger,
        IOptions<MyTunoScalingConfiguration> config,
        ApplicationDbContext dbContext)
    {
        _inventoryRepository = inventoryRepository;
        _characterRepository = characterRepository;
        _userManager = userManager;
        _logger = logger;
        _scalingConfig = config.Value;
        _gatheringConfig = config.Value.Gathering;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Uses a Fino to heal the user's character (25% HP)
    /// </summary>
    public async Task<(bool Success, int HealedAmount, string Message)> UseFinoAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await UseHealingItemAsync(userId, InventoryItemType.Fino, FinoHealPercentage, "Fino", cancellationToken);
    }

    /// <summary>
    /// Uses a Caneca to heal the user's character (50% HP)
    /// </summary>
    public async Task<(bool Success, int HealedAmount, string Message)> UseCanecaAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await UseHealingItemAsync(userId, InventoryItemType.Caneca, CanecaHealPercentage, "Caneca", cancellationToken);
    }

    /// <summary>
    /// Shared healing logic for Fino/Caneca
    /// </summary>
    private async Task<(bool Success, int HealedAmount, string Message)> UseHealingItemAsync(
        string userId, InventoryItemType itemType, double healPercentage, string itemName,
        CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, itemType, cancellationToken);
        if (item == null || item.Quantity <= 0)
        {
            return (false, 0, $"Não tens {itemName} no inventário");
        }

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use {Item} but has no character", userId, itemName);
            return (false, 0, "Personagem não encontrado");
        }

        var maxHp = character.ShotBuffBattlesRemaining > 0
            ? Character.CreateShotBuffedCopy(character).TotalHP
            : character.TotalHP;

        var currentHp = character.CurrentHP ?? maxHp;

        if (currentHp >= maxHp)
        {
            return (false, 0, "O personagem já está com HP máximo");
        }

        var healAmount = (int)Math.Round(maxHp * healPercentage);

        character.Heal(healAmount);
        await _characterRepository.UpdateAsync(character);

        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
        {
            _logger.LogError("Failed to consume {Item} for user {UserId}", itemName, userId);
            return (false, 0, $"Erro ao consumir {itemName}");
        }

        return (true, healAmount, $"Personagem curado! +{healAmount} HP");
    }

    /// <summary>
    /// Uses a Cigarro — shields the next 3 incoming hits (no damage taken)
    /// </summary>
    public async Task<(bool Success, string Message)> UseCigarroAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Cigarro, cancellationToken);
        if (item == null || item.Quantity <= 0)
        {
            return (false, "Não tens cigarros no inventário");
        }

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use cigarro but has no character", userId);
            return (false, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, "Não podes usar cigarro num personagem morto");
        }

        if (character.CigarroShieldHitsRemaining > 0)
        {
            return (false, "Já tens um escudo de cigarro ativo");
        }

        character.CigarroShieldHitsRemaining = CigarroShieldHits;
        await _characterRepository.UpdateAsync(character);

        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Cigarro, 1, cancellationToken);
        if (!consumed)
        {
            _logger.LogError("Failed to consume cigarro for user {UserId}", userId);
            return (false, "Erro ao consumir cigarro");
        }

        return (true, $"Cigarro ativado! Próximos {CigarroShieldHits} hits não causam dano");
    }

    /// <summary>
    /// Uses a Canhão — next 3 outgoing hits deal 30% more damage
    /// </summary>
    public async Task<(bool Success, string Message)> UseCanhaoAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Canhao, cancellationToken);
        if (item == null || item.Quantity <= 0)
        {
            return (false, "Não tens canhões no inventário");
        }

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use canhão but has no character", userId);
            return (false, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, "Não podes usar canhão num personagem morto");
        }

        if (character.CanhaoDamageBoostHitsRemaining > 0)
        {
            return (false, "Já tens um boost de canhão ativo");
        }

        character.CanhaoDamageBoostHitsRemaining = CanhaoDamageBoostHits;
        await _characterRepository.UpdateAsync(character);

        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Canhao, 1, cancellationToken);
        if (!consumed)
        {
            _logger.LogError("Failed to consume canhão for user {UserId}", userId);
            return (false, "Erro ao consumir canhão");
        }

        return (true, $"Canhão ativado! Próximos {CanhaoDamageBoostHits} ataques causam +30% dano");
    }

    /// <summary>
    /// Uses a Penalty — 0.5s attack speed + 100% crit for 1 run/battle
    /// </summary>
    public async Task<(bool Success, string Message)> UsePenaltyAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Penalty, cancellationToken);
        if (item == null || item.Quantity <= 0)
        {
            return (false, "Não tens penalties no inventário");
        }

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use penalty but has no character", userId);
            return (false, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, "Não podes usar penalty num personagem morto");
        }

        if (character.PenaltyBuffActive > 0)
        {
            return (false, "Já tens um penalty ativo");
        }

        character.PenaltyBuffActive = 1;
        await _characterRepository.UpdateAsync(character);

        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Penalty, 1, cancellationToken);
        if (!consumed)
        {
            _logger.LogError("Failed to consume penalty for user {UserId}", userId);
            return (false, "Erro ao consumir penalty");
        }

        return (true, "Penalty ativado! 0.5s ataque + 100% crit por 1 run/batalha");
    }

    /// <summary>
    /// Gets the quantity of Fino in the user's inventory
    /// </summary>
    public async Task<int> GetFinoQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Fino, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Caneca in the user's inventory
    /// </summary>
    public async Task<int> GetCanecaQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Caneca, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Cigarro in the user's inventory
    /// </summary>
    public async Task<int> GetCigarroQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Cigarro, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Canhão in the user's inventory
    /// </summary>
    public async Task<int> GetCanhaoQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Canhao, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Penalty in the user's inventory
    /// </summary>
    public async Task<int> GetPenaltyQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Penalty, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of shots in the user's inventory
    /// </summary>
    public async Task<int> GetShotQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var shotItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Shot, cancellationToken);
        return shotItem?.Quantity ?? 0;
    }

    // Shot empowers the next 5 arena battles or stage runs
    private const int ShotBuffBattles = 5;
    private const double ShotBuffMultiplier = 1.20; // 20% boost

    /// <summary>
    /// Uses a shot to empower the character's next 5 arena battles
    /// Increases all stats by 20% for the duration
    /// Also scales up CurrentHP proportionally to the new buffed max HP
    /// </summary>
    public async Task<(bool Success, int BattlesEmpowered, string Message)> UseShotAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Check if user has shot
        var shotItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Shot, cancellationToken);
        if (shotItem == null || shotItem.Quantity <= 0)
        {
            return (false, 0, "Não tens shots no inventário");
        }

        // Get user's character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use shot but has no character", userId);
            return (false, 0, "Personagem não encontrado");
        }

        // Check if character is dead
        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, 0, "Não podes usar shot num personagem morto");
        }

        // Check if already has active buff
        if (character.ShotBuffBattlesRemaining > 0)
        {
            return (false, 0, "Já tens um buff ativo");
        }

        // Apply the buff
        character.ShotBuffBattlesRemaining = ShotBuffBattles;
        
        // Scale up CurrentHP proportionally to the new buffed max HP
        // Use CreateShotBuffedCopy to get the EXACT same buffed max HP that will be used in arena
        var currentHpValue = character.CurrentHP ?? character.TotalHP;
        var unbuffedMaxHp = character.TotalHP;
        var buffedCopy = Character.CreateShotBuffedCopy(character);
        var buffedMaxHp = buffedCopy.TotalHP;
        
        // Calculate the new CurrentHP proportionally
        var hpRatio = (double)currentHpValue / unbuffedMaxHp;
        character.CurrentHP = (int)Math.Round(hpRatio * buffedMaxHp);
        
        await _characterRepository.UpdateAsync(character);

        // Consume 1 shot
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Shot, 1, cancellationToken);
        if (!consumed)
        {
            _logger.LogError("Failed to consume shot for user {UserId} even though quantity was checked", userId);
            return (false, 0, "Erro ao consumir shot");
        }

        return (true, ShotBuffBattles, "Shot ativado! +20% stats na próxima batalha");
    }

    /// <summary>
    /// Gets the energy cost for a resource type from config
    /// </summary>
    private int GetEnergyCost(InventoryItemType type)
    {
        var typeName = type.ToString();
        var resource = _gatheringConfig.Resources.FirstOrDefault(r => r.Type == typeName);
        return resource?.EnergyCost ?? int.MaxValue;
    }

    /// <summary>
    /// Gets the quantity of a resource in the user's inventory
    /// </summary>
    public async Task<int> GetResourceQuantityAsync(string userId, InventoryItemType type, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, type, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets all inventory item quantities for a user in a single query.
    /// Returns a dictionary of item type to quantity, replacing multiple individual Get*QuantityAsync calls.
    /// </summary>
    public async Task<Dictionary<InventoryItemType, int>> GetUserInventorySummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allItems = await _inventoryRepository.GetUserInventoryAsync(userId, cancellationToken);
        return allItems
            .Where(i => i.Quantity > 0)
            .ToDictionary(i => i.Type, i => i.Quantity);
    }

    /// <summary>
    /// Gets the current energy for an already-loaded character, applying passive regen since last check.
    /// Avoids a redundant character load when the caller already has the character.
    /// </summary>
    public async Task<(int CurrentEnergy, int MaxEnergy, int SecondsUntilNextRegen)> GetCurrentEnergyForCharacterAsync(Character character, CancellationToken cancellationToken = default)
    {
        if (character == null)
            return (0, 10, 0);

        ApplyEnergyRegen(character);
        await _characterRepository.UpdateAsync(character);

        var secondsUntilNext = 0;
        if (character.Energy < character.MaxEnergy)
        {
            var regenInterval = _gatheringConfig.RegenIntervalSeconds;
            if (regenInterval <= 0) regenInterval = 60;
            var lastRegen = character.LastEnergyRegenAt ?? DateTime.UtcNow;
            var elapsed = (DateTime.UtcNow - lastRegen).TotalSeconds;
            secondsUntilNext = Math.Max(1, regenInterval - (int)elapsed);
        }

        return (character.Energy, character.MaxEnergy, secondsUntilNext);
    }

    /// <summary>
    /// Gets the current energy for a character, applying passive regen since last check
    /// </summary>
    public async Task<(int CurrentEnergy, int MaxEnergy, int SecondsUntilNextRegen)> GetCurrentEnergyAsync(string userId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            return (0, 10, 0);
        }

        ApplyEnergyRegen(character);
        await _characterRepository.UpdateAsync(character);

        // Calculate seconds until next regen tick
        var secondsUntilNext = 0;
        if (character.Energy < character.MaxEnergy)
        {
            var regenInterval = _gatheringConfig.RegenIntervalSeconds;
            if (regenInterval <= 0) regenInterval = 60;
            var lastRegen = character.LastEnergyRegenAt ?? DateTime.UtcNow;
            var elapsed = (DateTime.UtcNow - lastRegen).TotalSeconds;
            secondsUntilNext = Math.Max(1, regenInterval - (int)elapsed);
        }

        return (character.Energy, character.MaxEnergy, secondsUntilNext);
    }

    /// <summary>
    /// Gathers a resource by spending energy
    /// </summary>
    public async Task<(bool Success, int Gathered, int RemainingEnergy, string Message)> GatherResourceAsync(string userId, InventoryItemType resourceType, CancellationToken cancellationToken = default)
    {
        // Validate resource type is active in config
        var typeName = resourceType.ToString();
        var resourceConfig = _gatheringConfig.Resources.FirstOrDefault(r => r.Type == typeName && r.IsActive);
        if (resourceConfig == null)
        {
            return (false, 0, 0, "Tipo de recurso inválido para destilação");
        }

        var energyCost = resourceConfig.EnergyCost;

        // Get character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to gather but has no character", userId);
            return (false, 0, 0, "Personagem não encontrado");
        }

        // Apply passive energy regen first
        ApplyEnergyRegen(character);

        // Check if enough energy
        if (character.Energy < energyCost)
        {
            return (false, 0, character.Energy, $"Energia insuficiente! Precisas de {energyCost} energia");
        }

        // Spend energy (don't reset LastEnergyRegenAt — preserve partial regen progress)
        character.Energy -= energyCost;
        await _characterRepository.UpdateAsync(character);

        // Add resource to inventory
        await _inventoryRepository.AddItemAsync(userId, resourceType, 1, cancellationToken);

        var resourceName = resourceType switch
        {
            InventoryItemType.Vodka => "Vodka",
            InventoryItemType.Gin => "Gin",
            InventoryItemType.Whisky => "Whisky",
            InventoryItemType.Absinto => "Absinto",
            _ => resourceType.ToString()
        };

        return (true, 1, character.Energy, $"+1 {resourceName}!");
    }

    /// <summary>
    /// Applies passive energy regeneration based on elapsed time since last regen
    /// 1 energy per regen interval (default 60 seconds), capped at MaxEnergy
    /// </summary>
    private void ApplyEnergyRegen(Character character)
    {
        if (character.Energy >= character.MaxEnergy)
        {
            character.LastEnergyRegenAt = DateTime.UtcNow;
            return;
        }

        var regenInterval = _gatheringConfig.RegenIntervalSeconds;
        if (regenInterval <= 0) regenInterval = 60;

        var lastRegen = character.LastEnergyRegenAt ?? DateTime.UtcNow;
        var elapsed = DateTime.UtcNow - lastRegen;
        var regenAmount = (int)(elapsed.TotalSeconds / regenInterval);

        if (regenAmount > 0)
        {
            character.Energy = Math.Min(character.MaxEnergy, character.Energy + regenAmount);
            // Keep remainder time by advancing lastRegen by the consumed ticks only
            character.LastEnergyRegenAt = lastRegen.AddSeconds(regenAmount * regenInterval);
        }
    }

    /// <summary>
    /// Gets all instrument part quantities for a user (items with type 100-112)
    /// </summary>
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

        // Set new item in slot
        switch (slot.Value)
        {
            case EquipmentSlot.Head: character.EquippedHead = itemType; break;
            case EquipmentSlot.Shoulders: character.EquippedShoulders = itemType; break;
            case EquipmentSlot.Chest: character.EquippedChest = itemType; break;
            case EquipmentSlot.Gloves: character.EquippedGloves = itemType; break;
            case EquipmentSlot.Legs: character.EquippedLegs = itemType; break;
            case EquipmentSlot.Boots: character.EquippedBoots = itemType; break;
        }

        // Return currently equipped item to inventory
        if (currentlyEquipped.HasValue)
        {
            await _inventoryRepository.AddItemAsync(userId, currentlyEquipped.Value, 1, cancellationToken);
        }

        // Consume 1 from inventory
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
            return (false, "Erro ao consumir item do inventário");

        RecalculateEquipmentBonuses(character);

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

        // Clear slot
        switch (slot.Value)
        {
            case EquipmentSlot.Head: character.EquippedHead = null; break;
            case EquipmentSlot.Shoulders: character.EquippedShoulders = null; break;
            case EquipmentSlot.Chest: character.EquippedChest = null; break;
            case EquipmentSlot.Gloves: character.EquippedGloves = null; break;
            case EquipmentSlot.Legs: character.EquippedLegs = null; break;
            case EquipmentSlot.Boots: character.EquippedBoots = null; break;
        }

        // Return to inventory
        await _inventoryRepository.AddItemAsync(userId, itemType, 1, cancellationToken);

        RecalculateEquipmentBonuses(character);

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

        // Determine Fidelis value (scales with player level)
        var discardValues = _scalingConfig.StageMode.DiscardValues;
        var baseValue = isEquipment ? discardValues.Equipment : discardValues.InstrumentPart;

        // Apply level scaling to discard value
        var character = await _dbContext.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        var level = character?.Level ?? 1;
        var discardScale = 1.0 + level * _scalingConfig.StageMode.DiscardLevelScale;
        var fidelisValue = Math.Round(baseValue * (decimal)discardScale, 2);

        // Consume 1 from inventory
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
            return (false, 0, "Erro ao descartar item");

        // Credit Fidelis to user
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.FidelisBalance += fidelisValue;
            await _userManager.UpdateAsync(user);
        }

        var displayName = isEquipment
            ? EquipmentDropHelper.GetDisplayName(EquipmentDropHelper.FromInventoryItemType(itemType)!.Value)
            : InstrumentTypeHelper.GetDisplayName(InstrumentTypeHelper.FromInventoryPartType(itemType)!.Value);

        return (true, fidelisValue, $"{displayName} descartado por {fidelisValue:F2} Fidelis!");
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

    public async Task<(bool Success, ForgedWeapon? Weapon, string Message)> ForgeWeaponAsync(
        string userId, InventoryItemType instrumentPart, InventoryItemType drink,
        WeaponType weaponType, string weaponName, CancellationToken cancellationToken = default)
    {
        if (!InstrumentTypeHelper.IsInstrumentPart(instrumentPart))
            return (false, null, "Item de instrumento inválido");

        if (!DrinkTypes.Contains(drink))
            return (false, null, "Bebida inválida");

        if (string.IsNullOrWhiteSpace(weaponName) || weaponName.Length > 100)
            return (false, null, "Nome da arma inválido (máx 100 caracteres)");

        var instrItem = await _inventoryRepository.GetItemAsync(userId, instrumentPart, cancellationToken);
        if (instrItem == null || instrItem.Quantity <= 0)
            return (false, null, "Não tens este instrumento no inventário");

        var drinkItem = await _inventoryRepository.GetItemAsync(userId, drink, cancellationToken);
        if (drinkItem == null || drinkItem.Quantity <= 0)
            return (false, null, "Não tens esta bebida no inventário");

        // Consume both materials
        var consumedInstr = await _inventoryRepository.ConsumeItemAsync(userId, instrumentPart, 1, cancellationToken);
        if (!consumedInstr)
            return (false, null, "Erro ao consumir instrumento");

        var consumedDrink = await _inventoryRepository.ConsumeItemAsync(userId, drink, 1, cancellationToken);
        if (!consumedDrink)
            return (false, null, "Erro ao consumir bebida");

        // Calculate weapon stats from config, scaled by drink energy cost
        var weaponStats = _scalingConfig.StageMode.EquipmentStats.Instrument;
        var forging = _scalingConfig.StageMode.Forging;
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == drink.ToString());
        var drinkCostMultiplier = drinkResource?.EnergyCost ?? 1;

        // Roll random instrument quality within configured range
        var random = new Random();
        var instrumentQuality = forging.InstrumentQualityMin +
            random.NextDouble() * (forging.InstrumentQualityMax - forging.InstrumentQualityMin);

        // 2H weapons get a multiplier to match dual-wielding 1H
        var isTwoHanded = WeaponTypeHelper.IsTwoHanded(weaponType);
        var handedMult = isTwoHanded ? forging.TwoHandedMultiplier : 1.0;

        var totalMult = drinkCostMultiplier * instrumentQuality * handedMult;

        var weapon = ForgedWeapon.Create(
            userId, weaponName, weaponType,
            instrumentPart, drink,
            bonusHP: (int)Math.Round(weaponStats.HP * totalMult),
            bonusPower: (int)Math.Round(weaponStats.Power * totalMult),
            bonusSpeed: (int)Math.Round(weaponStats.Speed * totalMult),
            bonusDefense: (int)Math.Round(weaponStats.Defense * totalMult),
            bonusCriticalChance: weaponStats.CriticalChance * drinkCostMultiplier * instrumentQuality);

        _dbContext.ForgedWeapons.Add(weapon);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, weapon, $"Arma forjada: {weaponName}!");
    }

    public async Task<List<ForgedWeapon>> GetForgedWeaponsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ForgedWeapons
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Success, string Message)> EquipWeaponAsync(string userId, int weaponId, int slot, CancellationToken cancellationToken = default)
    {
        if (slot != 1 && slot != 2)
            return (false, "Slot inválido");

        var weapon = await _dbContext.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return (false, "Personagem não encontrado");

        // Unequip any weapons currently in the target slot(s)
        if (weapon.IsTwoHanded)
        {
            // Two-handed fills both slots: unequip whatever is in slot 1 and 2
            await UnequipWeaponInternal(character, 1, cancellationToken);
            await UnequipWeaponInternal(character, 2, cancellationToken);
            character.EquippedWeapon1 = weaponId;
            character.EquippedWeapon2 = weaponId; // same weapon in both slots
        }
        else
        {
            // One-handed: if the other slot has a two-handed weapon, unequip it from both
            var otherSlot = slot == 1 ? 2 : 1;
            var otherWeaponId = slot == 1 ? character.EquippedWeapon2 : character.EquippedWeapon1;
            if (otherWeaponId.HasValue)
            {
                var otherWeapon = await _dbContext.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == otherWeaponId.Value, cancellationToken);
                if (otherWeapon?.IsTwoHanded == true)
                {
                    await UnequipWeaponInternal(character, 1, cancellationToken);
                    await UnequipWeaponInternal(character, 2, cancellationToken);
                }
            }

            await UnequipWeaponInternal(character, slot, cancellationToken);
            if (slot == 1) character.EquippedWeapon1 = weaponId;
            else character.EquippedWeapon2 = weaponId;
        }

        weapon.IsEquipped = true;
        RecalculateEquipmentBonuses(character, cancellationToken);
        await _characterRepository.UpdateAsync(character);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, $"{weapon.Name} equipado!");
    }

    public async Task<(bool Success, string Message)> UnequipWeaponAsync(string userId, int slot, CancellationToken cancellationToken = default)
    {
        if (slot != 1 && slot != 2)
            return (false, "Slot inválido");

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return (false, "Personagem não encontrado");

        var weaponId = slot == 1 ? character.EquippedWeapon1 : character.EquippedWeapon2;
        if (!weaponId.HasValue)
            return (false, "Nenhuma arma equipada neste slot");

        var weapon = await _dbContext.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId.Value, cancellationToken);

        // If two-handed, clear both slots
        if (weapon?.IsTwoHanded == true)
        {
            character.EquippedWeapon1 = null;
            character.EquippedWeapon2 = null;
        }
        else
        {
            if (slot == 1) character.EquippedWeapon1 = null;
            else character.EquippedWeapon2 = null;
        }

        if (weapon != null) weapon.IsEquipped = false;

        RecalculateEquipmentBonuses(character, cancellationToken);
        await _characterRepository.UpdateAsync(character);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, weapon != null ? $"{weapon.Name} desequipado!" : "Arma desequipada!");
    }

    private async Task UnequipWeaponInternal(Character character, int slot, CancellationToken cancellationToken)
    {
        var weaponId = slot == 1 ? character.EquippedWeapon1 : character.EquippedWeapon2;
        if (!weaponId.HasValue) return;

        var weapon = await _dbContext.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId.Value, cancellationToken);
        if (weapon != null) weapon.IsEquipped = false;

        if (slot == 1) character.EquippedWeapon1 = null;
        else character.EquippedWeapon2 = null;
    }

    /// <summary>
    /// Recalculates all equipment stat bonuses based on currently equipped items.
    /// Each character gets unique equipment quality per slot via deterministic seeding
    /// (characterId × 7919 + slotIndex × 31), giving variety across players without DB changes.
    /// </summary>
    private void RecalculateEquipmentBonuses(Character character, CancellationToken cancellationToken = default)
    {
        var stats = _scalingConfig.StageMode.EquipmentStats;
        var qualityMin = _scalingConfig.StageMode.EquipmentQualityMin;
        var qualityMax = _scalingConfig.StageMode.EquipmentQualityMax;
        var levelScale = 1.0 + character.Level * _scalingConfig.StageMode.EquipmentLevelScale;
        int hp = 0, power = 0, defense = 0;

        // Helper to get deterministic quality for this character + slot
        double GetSlotQuality(int slotIndex)
        {
            var rng = new Random(character.Id * 7919 + slotIndex * 31);
            return qualityMin + rng.NextDouble() * (qualityMax - qualityMin);
        }

        if (character.EquippedHead.HasValue) { var q = GetSlotQuality(0); hp += (int)Math.Round(stats.Head.HP * q * levelScale); power += (int)Math.Round(stats.Head.Power * q * levelScale); defense += (int)Math.Round(stats.Head.Defense * q * levelScale); }
        if (character.EquippedShoulders.HasValue) { var q = GetSlotQuality(1); hp += (int)Math.Round(stats.Shoulders.HP * q * levelScale); power += (int)Math.Round(stats.Shoulders.Power * q * levelScale); defense += (int)Math.Round(stats.Shoulders.Defense * q * levelScale); }
        if (character.EquippedChest.HasValue) { var q = GetSlotQuality(2); hp += (int)Math.Round(stats.Chest.HP * q * levelScale); power += (int)Math.Round(stats.Chest.Power * q * levelScale); defense += (int)Math.Round(stats.Chest.Defense * q * levelScale); }
        if (character.EquippedGloves.HasValue) { var q = GetSlotQuality(3); hp += (int)Math.Round(stats.Gloves.HP * q * levelScale); power += (int)Math.Round(stats.Gloves.Power * q * levelScale); defense += (int)Math.Round(stats.Gloves.Defense * q * levelScale); }
        if (character.EquippedLegs.HasValue) { var q = GetSlotQuality(4); hp += (int)Math.Round(stats.Legs.HP * q * levelScale); power += (int)Math.Round(stats.Legs.Power * q * levelScale); defense += (int)Math.Round(stats.Legs.Defense * q * levelScale); }
        if (character.EquippedBoots.HasValue) { var q = GetSlotQuality(5); hp += (int)Math.Round(stats.Boots.HP * q * levelScale); power += (int)Math.Round(stats.Boots.Power * q * levelScale); defense += (int)Math.Round(stats.Boots.Defense * q * levelScale); }

        // Add weapon bonuses from forged weapons
        var equippedWeaponIds = new HashSet<int>();
        if (character.EquippedWeapon1.HasValue) equippedWeaponIds.Add(character.EquippedWeapon1.Value);
        if (character.EquippedWeapon2.HasValue) equippedWeaponIds.Add(character.EquippedWeapon2.Value);

        if (equippedWeaponIds.Count > 0)
        {
            var weapons = _dbContext.ForgedWeapons
                .AsNoTracking()
                .Where(w => equippedWeaponIds.Contains(w.Id))
                .ToList();

            foreach (var w in weapons)
            {
                hp += w.BonusHP;
                power += w.BonusPower;
                defense += w.BonusDefense;
            }
        }

        character.EquipmentHPBonus = hp;
        character.EquipmentPowerBonus = power;
        character.EquipmentDefenseBonus = defense;
    }

    public decimal GetWeaponUpgradeCost(int currentLevel)
    {
        var forging = _scalingConfig.StageMode.Forging;
        return forging.WeaponUpgradeBaseCost * (decimal)Math.Pow((double)forging.WeaponUpgradeCostMultiplier, currentLevel);
    }

    public async Task<(bool Success, string Message)> UpgradeWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default)
    {
        var weapon = await _dbContext.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        var cost = GetWeaponUpgradeCost(weapon.Level);

        var character = await _dbContext.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || user.FidelisBalance < cost)
            return (false, $"Fidelis insuficiente (necessário: {cost:F2})");

        user.FidelisBalance -= cost;
        weapon.Level += 1;

        // Recalculate stats: increase base stats by upgrade bonus per level
        var forging = _scalingConfig.StageMode.Forging;
        var statBonus = forging.WeaponUpgradeStatBonus;
        var levelMultiplier = 1.0 + (weapon.Level * statBonus);
        var baseStats = _scalingConfig.StageMode.EquipmentStats.Instrument;

        // Find drink cost multiplier from the weapon's source drink
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString());
        var drinkCostMultiplier = drinkResource?.EnergyCost ?? 1;

        // 2H weapons get the two-handed multiplier to match dual-wielding 1H
        var handedMult = weapon.IsTwoHanded ? forging.TwoHandedMultiplier : 1.0;

        var totalMult = drinkCostMultiplier * levelMultiplier * handedMult;

        weapon.BonusHP = (int)Math.Round(baseStats.HP * totalMult);
        weapon.BonusPower = (int)Math.Round(baseStats.Power * totalMult);
        weapon.BonusDefense = (int)Math.Round(baseStats.Defense * totalMult);

        // Recalculate equipment bonuses if weapon is equipped
        if (weapon.IsEquipped)
        {
            RecalculateEquipmentBonuses(character, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, $"Arma melhorada para +{weapon.Level}!");
    }
}
