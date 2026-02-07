using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
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

    // Beer heals 25% of total HP
    private const double BeerHealPercentage = 0.25;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryService> logger,
        IOptions<MyTunoScalingConfiguration> config)
    {
        _inventoryRepository = inventoryRepository;
        _characterRepository = characterRepository;
        _userManager = userManager;
        _logger = logger;
        _scalingConfig = config.Value;
        _gatheringConfig = config.Value.Gathering;
    }

    /// <summary>
    /// Uses a beer to heal the user's character
    /// </summary>
    public async Task<(bool Success, int HealedAmount, string Message)> UseBeerAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Check if user has beer
        var beerItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Beer, cancellationToken);
        if (beerItem == null || beerItem.Quantity <= 0)
        {
            return (false, 0, "Não tens cervejas no inventário");
        }

        // Get user's character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use beer but has no character", userId);
            return (false, 0, "Personagem não encontrado");
        }

        // Calculate max HP (accounting for shot buff if active)
        var maxHp = character.ShotBuffBattlesRemaining > 0
            ? Character.CreateShotBuffedCopy(character).TotalHP
            : character.TotalHP;

        // Check if character is dead
        var currentHp = character.CurrentHP ?? maxHp;
        if (currentHp <= 0)
        {
            return (false, 0, "Não podes usar cerveja num personagem morto");
        }

        // Check if character needs healing
        if (currentHp >= maxHp)
        {
            return (false, 0, "O personagem já está com HP máximo");
        }

        // Calculate heal amount (25% of maxHP, rounded)
        var healAmount = (int)Math.Round(maxHp * BeerHealPercentage);

        // Heal the character
        character.Heal(healAmount);
        await _characterRepository.UpdateAsync(character);

        // Consume 1 beer
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, cancellationToken);
        if (!consumed)
        {
            _logger.LogError("Failed to consume beer for user {UserId} even though quantity was checked", userId);
            return (false, 0, "Erro ao consumir cerveja");
        }

        return (true, healAmount, $"Personagem curado! +{healAmount} HP");
    }

    /// <summary>
    /// Gets the quantity of beer in the user's inventory
    /// </summary>
    public async Task<int> GetBeerQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var beerItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Beer, cancellationToken);
        return beerItem?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of shots in the user's inventory
    /// </summary>
    public async Task<int> GetShotQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var shotItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Shot, cancellationToken);
        return shotItem?.Quantity ?? 0;
    }

    // Shot empowers the next 5 arena battles
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
            return (false, 0, $"Já tens um buff ativo ({character.ShotBuffBattlesRemaining} batalhas restantes)");
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

        return (true, ShotBuffBattles, $"Shot ativado! +20% stats nas próximas {ShotBuffBattles} batalhas");
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
        var isInstrument = InstrumentTypeHelper.IsInstrumentPart(itemType);
        if (!isEquipment && !isInstrument)
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
        InventoryItemType? currentlyEquipped = null;

        if (isEquipment)
        {
            var slot = EquipmentDropHelper.FromInventoryItemType(itemType);
            if (slot == null)
                return (false, "Slot de equipamento inválido");

            currentlyEquipped = slot.Value switch
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
        }
        else // instrument
        {
            currentlyEquipped = character.EquippedInstrument;
            character.EquippedInstrument = itemType;
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

        // Recalculate equipment stat bonuses
        RecalculateEquipmentBonuses(character);

        await _characterRepository.UpdateAsync(character);

        var displayName = isEquipment
            ? EquipmentDropHelper.GetDisplayName(EquipmentDropHelper.FromInventoryItemType(itemType)!.Value)
            : InstrumentTypeHelper.GetDisplayName(InstrumentTypeHelper.FromInventoryPartType(itemType)!.Value);

        return (true, $"{displayName} equipado!");
    }

    /// <summary>
    /// Unequips an item from a character slot and returns it to inventory.
    /// </summary>
    public async Task<(bool Success, string Message)> UnequipItemAsync(string userId, InventoryItemType itemType, CancellationToken cancellationToken = default)
    {
        var isEquipment = EquipmentDropHelper.IsEquipment(itemType);
        var isInstrument = InstrumentTypeHelper.IsInstrumentPart(itemType);
        if (!isEquipment && !isInstrument)
            return (false, "Este item não pode ser desequipado");

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return (false, "Personagem não encontrado");

        // Verify item is actually equipped
        InventoryItemType? currentlyEquipped = null;

        if (isEquipment)
        {
            var slot = EquipmentDropHelper.FromInventoryItemType(itemType);
            if (slot == null)
                return (false, "Slot de equipamento inválido");

            currentlyEquipped = slot.Value switch
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
        }
        else // instrument
        {
            currentlyEquipped = character.EquippedInstrument;
            if (currentlyEquipped != itemType)
                return (false, "Este instrumento não está equipado");

            character.EquippedInstrument = null;
        }

        // Return to inventory
        await _inventoryRepository.AddItemAsync(userId, itemType, 1, cancellationToken);

        // Recalculate equipment stat bonuses
        RecalculateEquipmentBonuses(character);

        await _characterRepository.UpdateAsync(character);

        var displayName = isEquipment
            ? EquipmentDropHelper.GetDisplayName(EquipmentDropHelper.FromInventoryItemType(itemType)!.Value)
            : InstrumentTypeHelper.GetDisplayName(InstrumentTypeHelper.FromInventoryPartType(itemType)!.Value);

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

        // Determine Fidelis value
        var discardValues = _scalingConfig.StageMode.DiscardValues;
        var fidelisValue = isEquipment ? discardValues.Equipment : discardValues.InstrumentPart;

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

    /// <summary>
    /// Recalculates all equipment stat bonuses based on currently equipped items.
    /// </summary>
    private void RecalculateEquipmentBonuses(Character character)
    {
        var stats = _scalingConfig.StageMode.EquipmentStats;
        int hp = 0, power = 0, speed = 0, defense = 0;
        double critical = 0;

        if (character.EquippedHead.HasValue) { hp += stats.Head.HP; power += stats.Head.Power; speed += stats.Head.Speed; defense += stats.Head.Defense; critical += stats.Head.CriticalChance; }
        if (character.EquippedShoulders.HasValue) { hp += stats.Shoulders.HP; power += stats.Shoulders.Power; speed += stats.Shoulders.Speed; defense += stats.Shoulders.Defense; critical += stats.Shoulders.CriticalChance; }
        if (character.EquippedChest.HasValue) { hp += stats.Chest.HP; power += stats.Chest.Power; speed += stats.Chest.Speed; defense += stats.Chest.Defense; critical += stats.Chest.CriticalChance; }
        if (character.EquippedGloves.HasValue) { hp += stats.Gloves.HP; power += stats.Gloves.Power; speed += stats.Gloves.Speed; defense += stats.Gloves.Defense; critical += stats.Gloves.CriticalChance; }
        if (character.EquippedLegs.HasValue) { hp += stats.Legs.HP; power += stats.Legs.Power; speed += stats.Legs.Speed; defense += stats.Legs.Defense; critical += stats.Legs.CriticalChance; }
        if (character.EquippedBoots.HasValue) { hp += stats.Boots.HP; power += stats.Boots.Power; speed += stats.Boots.Speed; defense += stats.Boots.Defense; critical += stats.Boots.CriticalChance; }
        if (character.EquippedInstrument.HasValue) { hp += stats.Instrument.HP; power += stats.Instrument.Power; speed += stats.Instrument.Speed; defense += stats.Instrument.Defense; critical += stats.Instrument.CriticalChance; }

        character.EquipmentHPBonus = hp;
        character.EquipmentPowerBonus = power;
        character.EquipmentSpeedBonus = speed;
        character.EquipmentDefenseBonus = defense;
        character.EquipmentCriticalBonus = critical;
    }
}
