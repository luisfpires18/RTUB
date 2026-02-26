using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private readonly IOptionsSnapshot<MyTunoScalingConfiguration> _scalingOptions;
    private MyTunoScalingConfiguration _scalingConfig => _scalingOptions.Value;
    private GatheringConfig _gatheringConfig => _scalingConfig.Gathering;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    // Per-user lock to prevent multi-tab energy exploits (race conditions on read-modify-write).
    // Static so it is shared across all scoped InventoryService instances (one per Blazor circuit).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _userEnergyLocks = new();
    private static SemaphoreSlim GetUserEnergyLock(string userId) => _userEnergyLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    // Fino heals 25% of total HP
    private const double FinoHealPercentage = 0.25;
    // Caneca heals 50% of total HP
    private const double CanecaHealPercentage = 0.50;
    // Cigarro grants +10% dodge for N runs
    private const int CigarroBuffRuns = 5;
    // Canhão grants AOE attacks for N runs
    // Canhão and Penalty are now timed — durations configured in scaling.config.json

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryService> logger,
        IOptionsSnapshot<MyTunoScalingConfiguration> config,
        IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _inventoryRepository = inventoryRepository;
        _characterRepository = characterRepository;
        _userManager = userManager;
        _logger = logger;
        _scalingOptions = config;
        _contextFactory = contextFactory;
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
        // 1. Validate character (read-only checks)
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

        // 2. Atomically consume item (prevents TOCTOU race — single SQL with WHERE Quantity >= 1)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
        {
            return (false, 0, $"Não tens {itemName} no inventário");
        }

        // 3. Apply heal effect (only after successful consume)
        var healAmount = (int)Math.Round(maxHp * healPercentage);

        character.Heal(healAmount);
        await _characterRepository.UpdateAsync(character);

        return (true, healAmount, $"Personagem curado! +{healAmount} HP");
    }

    /// <summary>
    /// Uses a Cigarro — shields the next 3 incoming hits (no damage taken)
    /// </summary>
    public async Task<(bool Success, string Message)> UseCigarroAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
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
            return (false, "Já tens um cigarro ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Cigarro, 1, cancellationToken);
        if (!consumed)
        {
            return (false, "Não tens cigarros no inventário");
        }

        // 3. Apply effect (only after successful consume)
        character.CigarroShieldHitsRemaining = CigarroBuffRuns;
        await _characterRepository.UpdateAsync(character);

        return (true, $"Cigarro ativado! +{(int)(character.EffectiveCigarroDodgeChance * 100)}% dodge por {CigarroBuffRuns} runs");
    }

    /// <summary>
    /// Uses a Canhão — next 3 outgoing hits deal 30% more damage
    /// </summary>
    public async Task<(bool Success, string Message)> UseCanhaoAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
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

        if (character.HasCanhaoBuff)
        {
            return (false, "Já tens um canhão ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Canhao, 1, cancellationToken);
        if (!consumed)
        {
            return (false, "Não tens canhões no inventário");
        }

        // 3. Apply effect (only after successful consume)
        // Store as paused remaining ms — the timer only ticks during active stage runs.
        // ResumeCanhaoBuff() is called when a stage run begins.
        var canhaoMinutes = character.EffectiveCanhaoMinutes;
        character.CanhaoBuffExpiresAt = null;
        character.CanhaoBuffRemainingMs = (long)TimeSpan.FromMinutes(canhaoMinutes).TotalMilliseconds;
        await _characterRepository.UpdateAsync(character);

        return (true, $"Canhão ativado! AOE por {canhaoMinutes} minutos");
    }

    /// <summary>
    /// Uses a Penalty — 0.5s attack speed + 100% crit for 1 run/battle
    /// </summary>
    public async Task<(bool Success, string Message)> UsePenaltyAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
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

        if (character.HasPenaltyBuff)
        {
            return (false, "Já tens um penalty ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Penalty, 1, cancellationToken);
        if (!consumed)
        {
            return (false, "Não tens penalties no inventário");
        }

        // 3. Apply effect (only after successful consume)
        // Store as paused state (RemainingMs) so the timer only ticks inside active runs
        var penaltyMinutes = character.EffectivePenaltyMinutes;
        var totalMs = (long)TimeSpan.FromMinutes(penaltyMinutes).TotalMilliseconds;
        character.PenaltyBuffRemainingMs = totalMs;
        character.PenaltyBuffExpiresAt = null;
        await _characterRepository.UpdateAsync(character);

        return (true, $"Penalty ativado! {(character.EffectivePenaltyLifesteal * 100):F1}% lifesteal por {penaltyMinutes} minutos");
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
    private const double ShotBuffMultiplier = 1.05; // 5% boost

    /// <summary>
    /// Uses a shot to empower the character's next 5 arena battles
    /// Increases all stats by 20% for the duration
    /// Also scales up CurrentHP proportionally to the new buffed max HP
    /// </summary>
    public async Task<(bool Success, int BattlesEmpowered, string Message)> UseShotAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use shot but has no character", userId);
            return (false, 0, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, 0, "Não podes usar shot num personagem morto");
        }

        if (character.ShotBuffBattlesRemaining > 0)
        {
            return (false, 0, "Já tens um buff ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Shot, 1, cancellationToken);
        if (!consumed)
        {
            return (false, 0, "Não tens shots no inventário");
        }

        // 3. Apply buff effect (only after successful consume)
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

        return (true, ShotBuffBattles, $"Shot ativado! +{(int)((character.EffectiveShotBuffMultiplier - 1) * 100)}% stats por {ShotBuffBattles} runs");
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

        var secondsUntilNext = 0;
        if (character.Energy < character.MaxEnergy)
        {
            var regenInterval = (int)character.EffectiveRegenInterval;
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

        // Calculate seconds until next regen tick
        var secondsUntilNext = 0;
        if (character.Energy < character.MaxEnergy)
        {
            var regenInterval = (int)character.EffectiveRegenInterval;
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

        // Acquire per-user lock to prevent multi-tab race conditions.
        // Without this, two tabs can both read Energy=10, both deduct, and both save Energy=9
        // instead of the correct 10→9→8 sequence.
        var userLock = GetUserEnergyLock(userId);
        await userLock.WaitAsync(cancellationToken);
        try
        {
            // Force a fresh read from DB inside the lock to pick up changes from other circuits
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

            // Check for double gathering (Destilaria improvement)
            var doubleChance = character.DoubleGatheringChance;
            var isDouble = doubleChance > 0 && Random.Shared.NextDouble() < doubleChance;
            var gatherAmount = isDouble ? 2 : 1;

            // Add resource to inventory
            await _inventoryRepository.AddItemAsync(userId, resourceType, gatherAmount, cancellationToken);

            var resourceName = resourceType switch
            {
                InventoryItemType.Vodka => "Vodka",
                InventoryItemType.Gin => "Gin",
                InventoryItemType.Whisky => "Whisky",
                InventoryItemType.Absinto => "Absinto",
                _ => resourceType.ToString()
            };

            var message = isDouble
                ? $"🔥 DUPLO! +{gatherAmount} {resourceName}!"
                : $"+1 {resourceName}!";

            return (true, gatherAmount, character.Energy, message);
        }
        finally
        {
            userLock.Release();
        }
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

        var regenInterval = (int)character.EffectiveRegenInterval;
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

        // Determine how many drinks this tier requires
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == drink.ToString());
        var forgeCost = drinkResource?.ForgeCost ?? 1;

        var drinkItem = await _inventoryRepository.GetItemAsync(userId, drink, cancellationToken);
        if (drinkItem == null || drinkItem.Quantity < forgeCost)
            return (false, null, $"Precisas de {forgeCost}x {drinkResource?.Name ?? drink.ToString()} (tens {drinkItem?.Quantity ?? 0})");

        // Calculate weapon stats from config, scaled by drink tier
        var weaponStats = _scalingConfig.StageMode.EquipmentStats.Instrument;
        var forging = _scalingConfig.StageMode.Forging;
        var drinkEnergyCost = drinkResource?.EnergyCost ?? 1;

        // Drink tier multiplier: higher-tier drinks produce stronger weapons
        // Formula: 1.0 + (energyCost - 1) * bonusPerTier → Cerveja=1.0×, Vinho=1.25×, … Aguardente=3.25×
        var drinkTierMult = 1.0 + (drinkEnergyCost - 1) * forging.DrinkStatBonusPerTier;

        // Roll random instrument quality within configured range
        var instrumentQuality = forging.InstrumentQualityMin +
            Random.Shared.NextDouble() * (forging.InstrumentQualityMax - forging.InstrumentQualityMin);

        // 2H weapons get a multiplier to match dual-wielding 1H
        var isTwoHanded = WeaponTypeHelper.IsTwoHanded(weaponType);
        var handedMult = isTwoHanded ? forging.TwoHandedMultiplier : 1.0;

        var totalMult = drinkTierMult * instrumentQuality * handedMult;

        // Roll crit and speed for high-tier drinks (unique, forge-time only)
        // 2H weapons get two independent rolls to match dual-wielding two 1H weapons
        var rollCount = isTwoHanded ? 2 : 1;

        var bonusCrit = 0.0;
        for (int i = 0; i < rollCount; i++)
        {
            if (drinkEnergyCost >= forging.CritMinDrinkCost && Random.Shared.NextDouble() < forging.CritRollChance)
            {
                bonusCrit += forging.CritMin + Random.Shared.NextDouble() * (forging.CritMax - forging.CritMin);
            }
        }
        bonusCrit = Math.Round(bonusCrit, 3);

        var bonusSpeed = 0;
        for (int i = 0; i < rollCount; i++)
        {
            if (drinkEnergyCost >= forging.SpeedMinDrinkCost && Random.Shared.NextDouble() < forging.SpeedRollChance)
            {
                bonusSpeed += Random.Shared.Next(forging.SpeedMin, forging.SpeedMax + 1);
            }
        }

        var weapon = ForgedWeapon.Create(
            userId, weaponName, weaponType,
            instrumentPart, drink,
            bonusHP: (int)Math.Round(weaponStats.HP * totalMult),
            bonusPower: (int)Math.Round(weaponStats.Power * totalMult),
            bonusSpeed: bonusSpeed,
            bonusDefense: (int)Math.Round(weaponStats.Defense * totalMult),
            bonusCriticalChance: bonusCrit);

        // Atomic transaction: consume both materials + create weapon in a single commit.
        // Prevents material loss if drink consume or weapon insert fails after instrument is consumed.
        var ctx = _contextFactory.CreateDbContext();
        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);
        var instrRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InventoryItems SET Quantity = Quantity - 1 WHERE UserId = {userId} AND Type = {(int)instrumentPart} AND Quantity >= 1",
            cancellationToken);
        if (instrRows == 0)
            return (false, null, "Erro ao consumir instrumento");

        // Atomically consume drink
        var drinkRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InventoryItems SET Quantity = Quantity - {forgeCost} WHERE UserId = {userId} AND Type = {(int)drink} AND Quantity >= {forgeCost}",
            cancellationToken);
        if (drinkRows == 0)
            return (false, null, "Erro ao consumir bebida");

        ctx.ForgedWeapons.Add(weapon);
        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return (true, weapon, $"Arma forjada: {weaponName}!");
    }

    public async Task<List<ForgedWeapon>> GetForgedWeaponsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        return await ctx.ForgedWeapons
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Success, string Message)> EquipWeaponAsync(string userId, int weaponId, int slot, CancellationToken cancellationToken = default)
    {
        if (slot != 1 && slot != 2)
            return (false, "Slot inválido");

        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        // Unequip any weapons currently in the target slot(s)
        if (weapon.IsTwoHanded)
        {
            // Two-handed fills both slots: unequip whatever is in slot 1 and 2
            await UnequipWeaponInternal(character, 1, ctx, cancellationToken);
            await UnequipWeaponInternal(character, 2, ctx, cancellationToken);
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
                var otherWeapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == otherWeaponId.Value, cancellationToken);
                if (otherWeapon?.IsTwoHanded == true)
                {
                    await UnequipWeaponInternal(character, 1, ctx, cancellationToken);
                    await UnequipWeaponInternal(character, 2, ctx, cancellationToken);
                }
            }

            await UnequipWeaponInternal(character, slot, ctx, cancellationToken);
            if (slot == 1) character.EquippedWeapon1 = weaponId;
            else character.EquippedWeapon2 = weaponId;
        }

        weapon.IsEquipped = true;
        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        await ctx.SaveChangesAsync(cancellationToken);

        return (true, $"{weapon.Name} equipado!");
    }

    public async Task<(bool Success, string Message)> UnequipWeaponAsync(string userId, int slot, CancellationToken cancellationToken = default)
    {
        if (slot != 1 && slot != 2)
            return (false, "Slot inválido");

        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var weaponId = slot == 1 ? character.EquippedWeapon1 : character.EquippedWeapon2;
        if (!weaponId.HasValue)
            return (false, "Nenhuma arma equipada neste slot");

        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId.Value, cancellationToken);

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

        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        await ctx.SaveChangesAsync(cancellationToken);

        return (true, weapon != null ? $"{weapon.Name} desequipado!" : "Arma desequipada!");
    }

    private async Task UnequipWeaponInternal(Character character, int slot, ApplicationDbContext ctx, CancellationToken cancellationToken)
    {
        var weaponId = slot == 1 ? character.EquippedWeapon1 : character.EquippedWeapon2;
        if (!weaponId.HasValue) return;

        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId.Value, cancellationToken);
        if (weapon != null) weapon.IsEquipped = false;

        if (slot == 1) character.EquippedWeapon1 = null;
        else character.EquippedWeapon2 = null;
    }

    /// <summary>
    /// Public entry point to recalculate all equipment bonuses for a user's character.
    /// Call this on page load to ensure bonuses reflect current formula/config.
    /// </summary>
    public async Task RecalculateEquipmentBonusesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null) return;

        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recalculates all equipment stat bonuses based on currently equipped items.
    /// Each character gets unique equipment quality per slot via deterministic seeding
    /// (characterId × 7919 + slotIndex × 31), giving variety across players without DB changes.
    /// </summary>
    private async Task RecalculateEquipmentBonusesAsync(Character character, CancellationToken cancellationToken = default, ApplicationDbContext? ctx = null)
    {
        var stats = _scalingConfig.StageMode.EquipmentStats;
        var levelScale = 1.0 + character.Level * _scalingConfig.StageMode.EquipmentLevelScale;

        // Flat per-level bonuses (match stat upgrade flat bonuses so equipment levels feel equivalent)
        var hpPerLvl = _scalingConfig.StageMode.EquipmentHpPerLevel;
        var powPerLvl = _scalingConfig.StageMode.EquipmentPowerPerLevel;
        var defPerLvl = _scalingConfig.StageMode.EquipmentDefensePerLevel;

        int hp = 0, power = 0, defense = 0;

        // All 6 armor pieces are permanently equipped — always compute bonuses.
        // Quality defaults to 1.0 for characters created after the refactor.
        // Legacy characters with quality 0 fall back to 1.0.
        double GetQ(double stored) => stored > 0 ? stored : 1.0;

        // New formula: (baseStat + bonusLevel × flatPerLevel) × quality × levelScale
        void AddSlot(EquipmentPieceStats baseStats, double quality, int bonusLevel)
        {
            var q = GetQ(quality);
            hp += (int)Math.Round((baseStats.HP + bonusLevel * hpPerLvl) * q * levelScale);
            power += (int)Math.Round((baseStats.Power + bonusLevel * powPerLvl) * q * levelScale);
            defense += (int)Math.Round((baseStats.Defense + bonusLevel * defPerLvl) * q * levelScale);
        }

        AddSlot(stats.Head, character.EquippedHeadQuality, character.GetSlotBonusLevel(EquipmentSlot.Head));
        AddSlot(stats.Shoulders, character.EquippedShouldersQuality, character.GetSlotBonusLevel(EquipmentSlot.Shoulders));
        AddSlot(stats.Chest, character.EquippedChestQuality, character.GetSlotBonusLevel(EquipmentSlot.Chest));
        AddSlot(stats.Gloves, character.EquippedGlovesQuality, character.GetSlotBonusLevel(EquipmentSlot.Gloves));
        AddSlot(stats.Legs, character.EquippedLegsQuality, character.GetSlotBonusLevel(EquipmentSlot.Legs));
        AddSlot(stats.Boots, character.EquippedBootsQuality, character.GetSlotBonusLevel(EquipmentSlot.Boots));

        // Add weapon bonuses from forged weapons (with character level scaling)
        var weaponLevelScale = 1.0 + character.Level * _scalingConfig.StageMode.WeaponCharacterLevelScale;

        var equippedWeaponIds = new HashSet<int>();
        if (character.EquippedWeapon1.HasValue) equippedWeaponIds.Add(character.EquippedWeapon1.Value);
        if (character.EquippedWeapon2.HasValue) equippedWeaponIds.Add(character.EquippedWeapon2.Value);

        int speed = 0;
        double critChance = 0;

        if (equippedWeaponIds.Count > 0)
        {
            var weaponCtx = ctx ?? _contextFactory.CreateDbContext();
            // When caller provides a context, use tracking to see in-memory changes (e.g. after upgrade);
            // otherwise use AsNoTracking for standalone reads.
            var weaponQuery = weaponCtx.ForgedWeapons.Where(w => equippedWeaponIds.Contains(w.Id));
            var weapons = ctx != null
                ? await weaponQuery.ToListAsync(cancellationToken)
                : await weaponQuery.AsNoTracking().ToListAsync(cancellationToken);

            foreach (var w in weapons)
            {
                hp += (int)Math.Round(w.BonusHP * weaponLevelScale);
                power += (int)Math.Round(w.BonusPower * weaponLevelScale);
                defense += (int)Math.Round(w.BonusDefense * weaponLevelScale);
                speed += w.BonusSpeed;
                critChance += w.BonusCriticalChance;
            }
        }

        character.EquipmentHPBonus = hp;
        character.EquipmentPowerBonus = power;
        character.EquipmentDefenseBonus = defense;
        character.EquipmentSpeedBonus = speed;
        character.EquipmentCriticalBonus = critChance;
    }

    public decimal GetWeaponUpgradeCost(int currentLevel)
    {
        var forging = _scalingConfig.StageMode.Forging;
        return forging.WeaponUpgradeBaseCost + currentLevel * forging.WeaponUpgradeCostPerLevel;
    }

    /// <summary>
    /// Ordered list of drink types from lowest to highest tier, matching gathering resource order.
    /// </summary>
    private static readonly InventoryItemType[] DrinkTierOrder = new[]
    {
        InventoryItemType.Cerveja, InventoryItemType.Vinho, InventoryItemType.Licor,
        InventoryItemType.Rum, InventoryItemType.Tequilla, InventoryItemType.Vodka,
        InventoryItemType.Gin, InventoryItemType.Whisky, InventoryItemType.Absinto,
        InventoryItemType.Aguardente
    };

    /// <summary>
    /// Calculates ALL drink requirements for a WEAPON upgrade at a given level.
    /// Weapons now require ALL drink tiers from tier 0 through the current tier (cumulative).
    /// Previous tiers stay at max quantity (perTier), current tier scales from 1 to perTier.
    /// </summary>
    public List<(InventoryItemType DrinkType, int Quantity)> GetUpgradeDrinkRequirement(int currentLevel)
    {
        var perTier = _scalingConfig.StageMode.Forging.UpgradeLevelsPerDrinkTier;
        if (perTier < 1) perTier = 5;
        var tierIndex = Math.Min(currentLevel / perTier, DrinkTierOrder.Length - 1);
        var currentTierQty = (currentLevel % perTier) + 1;
        if (currentLevel / perTier >= DrinkTierOrder.Length)
            currentTierQty = perTier;

        var requirements = new List<(InventoryItemType DrinkType, int Quantity)>();
        for (int i = 0; i <= tierIndex; i++)
        {
            // Previous tiers locked at perTier, current tier scales 1→perTier
            var qty = i < tierIndex ? perTier : currentTierQty;
            requirements.Add((DrinkTierOrder[i], qty));
        }
        return requirements;
    }

    /// <summary>
    /// Calculates ALL drink requirements for an EQUIPMENT upgrade at a given level.
    /// Equipment requires ALL drink tiers from tier 0 through the current tier (cumulative).
    /// Previous tiers stay at max quantity (perTier), current tier scales from 1 to perTier.
    /// Example at level 102 (tier 2, perTier=50): Cerveja ×50, Vinho ×50, Licor ×3.
    /// </summary>
    public List<(InventoryItemType DrinkType, int Quantity)> GetEquipmentUpgradeDrinkRequirements(int currentLevel)
    {
        var perTier = _scalingConfig.StageMode.Forging.UpgradeLevelsPerDrinkTier;
        if (perTier < 1) perTier = 5;
        var tierIndex = Math.Min(currentLevel / perTier, DrinkTierOrder.Length - 1);
        var currentTierQty = (currentLevel % perTier) + 1;
        if (currentLevel / perTier >= DrinkTierOrder.Length)
            currentTierQty = perTier;

        var requirements = new List<(InventoryItemType DrinkType, int Quantity)>();
        for (int i = 0; i <= tierIndex; i++)
        {
            // Previous tiers locked at perTier, current tier scales 1→perTier
            var qty = i < tierIndex ? perTier : currentTierQty;
            requirements.Add((DrinkTierOrder[i], qty));
        }
        return requirements;
    }

    public async Task<(bool Success, string Message)> UpgradeWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        var cost = GetWeaponUpgradeCost(weapon.Level);

        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || user.FidelisBalance < cost)
            return (false, $"Fidelis insuficiente (necessário: {cost:F2})");

        // Require ALL drinks from tier 0 through current tier (cumulative)
        var drinkRequirements = GetUpgradeDrinkRequirement(weapon.Level);

        // Validate all drinks are available before consuming any (fail-fast)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var drinkItem = await _inventoryRepository.GetItemAsync(userId, drinkType, cancellationToken);
            var drinkRes = _scalingConfig.Gathering.Resources.FirstOrDefault(r => r.Type == drinkType.ToString());
            var drinkName = drinkRes?.Name ?? drinkType.ToString();
            if (drinkItem == null || drinkItem.Quantity < drinkQty)
                return (false, $"Precisas de {drinkQty}x {drinkName} (tens {drinkItem?.Quantity ?? 0})");
        }

        // Check Leitão cost (mid-game currency from Boss Mode)
        var piggies = _scalingConfig.BossMode.Piggies;
        var leitaoCost = PiggiesCostConfig.CalculateCost(
            weapon.Level, piggies.WeaponUpgradeStartLevel,
            piggies.WeaponUpgradeBaseCost, piggies.WeaponUpgradeCostEveryNLevels);

        if (leitaoCost > 0)
        {
            var leitaoItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Leitao, cancellationToken);
            if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                return (false, $"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");
        }

        // Atomic transaction: consume all drinks + optional Leitão + deduct Fidelis + upgrade weapon
        // in a single commit. Prevents material loss if any step fails mid-way.
        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Consume all drinks via raw SQL on the same context (atomic with WHERE guard)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var rows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {drinkQty} WHERE UserId = {userId} AND Type = {(int)drinkType} AND Quantity >= {drinkQty}",
                cancellationToken);
            if (rows == 0)
                return (false, "Erro ao consumir bebida");
        }

        // Consume Leitão if required
        if (leitaoCost > 0)
        {
            var leitaoRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {leitaoCost} WHERE UserId = {userId} AND Type = {(int)InventoryItemType.Leitao} AND Quantity >= {leitaoCost}",
                cancellationToken);
            if (leitaoRows == 0)
                return (false, "Erro ao consumir Leitões");
        }

        user.FidelisBalance -= cost;
        weapon.Level += 1;

        // Recalculate stats: flat bonus per level (matches equipment/stat upgrade model)
        var hpPerLvl = _scalingConfig.StageMode.EquipmentHpPerLevel;
        var powPerLvl = _scalingConfig.StageMode.EquipmentPowerPerLevel;
        var defPerLvl = _scalingConfig.StageMode.EquipmentDefensePerLevel;
        var forging = _scalingConfig.StageMode.Forging;
        var baseStats = _scalingConfig.StageMode.EquipmentStats.Instrument;

        // Find drink tier multiplier from the weapon's source drink
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString());
        var drinkEnergyCost = drinkResource?.EnergyCost ?? 1;
        var drinkTierMult = 1.0 + (drinkEnergyCost - 1) * forging.DrinkStatBonusPerTier;

        // 2H weapons get the two-handed multiplier to match dual-wielding 1H
        var handedMult = weapon.IsTwoHanded ? forging.TwoHandedMultiplier : 1.0;

        var scaleMult = drinkTierMult * handedMult;

        weapon.BonusHP = (int)Math.Round((baseStats.HP + weapon.Level * hpPerLvl) * scaleMult);
        weapon.BonusPower = (int)Math.Round((baseStats.Power + weapon.Level * powPerLvl) * scaleMult);
        weapon.BonusDefense = (int)Math.Round((baseStats.Defense + weapon.Level * defPerLvl) * scaleMult);

        // Recalculate equipment bonuses if weapon is equipped
        if (weapon.IsEquipped)
        {
            await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        }

        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return (true, $"Arma melhorada para +{weapon.Level}!");
    }

    /// <summary>
    /// Gets the enhancement level for a specific equipment slot (manual upgrades only).
    /// </summary>
    public async Task<int> GetSlotEnhancementLevelAsync(string userId, EquipmentSlot slot, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        return character?.GetSlotBonusLevel(slot) ?? 0;
    }

    /// <summary>
    /// Gets the Fidelis cost to upgrade equipment enhancement to the next level.
    /// Formula: baseCost + currentBonusLevel * costPerLevel
    /// </summary>
    public decimal GetEquipmentUpgradeCost(int currentBonusLevel)
    {
        var forging = _scalingConfig.StageMode.Forging;
        return Math.Round(forging.EquipmentUpgradeBaseCost + currentBonusLevel * forging.EquipmentUpgradeCostPerLevel, 2);
    }

    /// <summary>
    /// Upgrades a specific equipment slot's enhancement level by 1. Costs Fidelis.
    /// The slot bonus level is stored on the Character entity.
    /// </summary>
    public async Task<(bool Success, string Message)> UpgradeEquipmentSlotAsync(string userId, EquipmentSlot slot, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var currentSlotLevel = character.GetSlotBonusLevel(slot);
        var cost = GetEquipmentUpgradeCost(currentSlotLevel);

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || user.FidelisBalance < cost)
            return (false, $"Fidelis insuficiente (necessário: {cost:F2})");

        // Require ALL drinks from tier 0 through current tier (cumulative)
        var drinkRequirements = GetEquipmentUpgradeDrinkRequirements(currentSlotLevel);

        // Validate all drinks are available before consuming any (fail-fast)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var drinkItem = await _inventoryRepository.GetItemAsync(userId, drinkType, cancellationToken);
            var drinkRes = _scalingConfig.Gathering.Resources.FirstOrDefault(r => r.Type == drinkType.ToString());
            var drinkName = drinkRes?.Name ?? drinkType.ToString();
            if (drinkItem == null || drinkItem.Quantity < drinkQty)
                return (false, $"Precisas de {drinkQty}x {drinkName} (tens {drinkItem?.Quantity ?? 0})");
        }

        // Check Leitão cost (mid-game currency from Boss Mode)
        var piggies = _scalingConfig.BossMode.Piggies;
        var leitaoCost = PiggiesCostConfig.CalculateCost(
            currentSlotLevel, piggies.EquipmentUpgradeStartLevel,
            piggies.EquipmentUpgradeBaseCost, piggies.EquipmentUpgradeCostEveryNLevels);

        if (leitaoCost > 0)
        {
            var leitaoItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Leitao, cancellationToken);
            if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                return (false, $"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");
        }

        // Atomic transaction: consume all drinks + optional Leitão + deduct Fidelis + upgrade slot
        // in a single commit. Prevents material loss if any step fails mid-way.
        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Consume all drinks via raw SQL on the same context (atomic with WHERE guard)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var rows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {drinkQty} WHERE UserId = {userId} AND Type = {(int)drinkType} AND Quantity >= {drinkQty}",
                cancellationToken);
            if (rows == 0)
                return (false, "Erro ao consumir bebida");
        }

        // Consume Leitão if required
        if (leitaoCost > 0)
        {
            var leitaoRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {leitaoCost} WHERE UserId = {userId} AND Type = {(int)InventoryItemType.Leitao} AND Quantity >= {leitaoCost}",
                cancellationToken);
            if (leitaoRows == 0)
                return (false, "Erro ao consumir Leitões");
        }

        user.FidelisBalance -= cost;
        character.SetSlotBonusLevel(slot, currentSlotLevel + 1);

        // Recalculate equipment bonuses with the new per-slot enhancement level
        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);

        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        var newLevel = currentSlotLevel + 1;
        var slotName = slot.ToString().ToUpperInvariant();
        return (true, $"{slotName} melhorado para +{newLevel}!");
    }

    /// <summary>
    /// Calculates the Fidelis value for discarding a forged weapon.
    /// Formula: weaponDiscardBase × (1 + weaponLevel × 0.5) × drinkCostMultiplier × (1 + charLevel × discardLevelScale)
    /// </summary>
    public decimal GetWeaponDiscardValue(ForgedWeapon weapon, int characterLevel)
    {
        var discardBase = _scalingConfig.StageMode.DiscardValues.Weapon;
        var discardLevelScale = _scalingConfig.StageMode.DiscardLevelScale;

        // Scale with weapon enhancement level
        var weaponLevelMult = 1.0 + weapon.Level * 0.5;

        // Scale with drink rarity (higher tier drinks = more valuable weapons)
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString());
        var drinkCostMultiplier = drinkResource?.EnergyCost ?? 1;

        // Scale with character level
        var charLevelMult = 1.0 + characterLevel * (double)discardLevelScale;

        return Math.Round(discardBase * (decimal)(weaponLevelMult * drinkCostMultiplier * charLevelMult), 2);
    }

    /// <summary>
    /// Discards a forged weapon in exchange for Fidelis currency.
    /// Unequips the weapon first if it is currently equipped.
    /// </summary>
    public async Task<(bool Success, decimal FidelisGained, string Message)> DiscardWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons
            .FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);

        if (weapon == null)
            return (false, 0, "Arma não encontrada");

        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // Unequip weapon if currently equipped
        if (weapon.IsEquipped && character != null)
        {
            if (character.EquippedWeapon1 == weaponId)
                character.EquippedWeapon1 = null;
            if (character.EquippedWeapon2 == weaponId)
                character.EquippedWeapon2 = null;
        }

        // Calculate discard value
        var charLevel = character?.Level ?? 1;
        var fidelisValue = GetWeaponDiscardValue(weapon, charLevel);

        var weaponName = weapon.Name;

        // Remove weapon from database
        ctx.ForgedWeapons.Remove(weapon);

        // Credit Fidelis to user
        var user = await ctx.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null)
        {
            user.FidelisBalance += fidelisValue;
        }

        // Recalculate equipment bonuses if weapon was equipped
        if (weapon.IsEquipped && character != null)
        {
            await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        }

        await ctx.SaveChangesAsync(cancellationToken);

        return (true, fidelisValue, $"{weaponName} descartada por {fidelisValue:F2} Fidelis!");
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ApplyRareSetUpgradeAsync(string userId, InventoryItemType rareItemType, CancellationToken cancellationToken = default)
    {
        // Validate the item is a rare set piece
        var slot = EquipmentDropHelper.FromRareInventoryItemType(rareItemType);
        if (slot == null)
            return (false, "Item inválido — não é uma peça de conjunto raro.");

        var ctx = _contextFactory.CreateDbContext();

        // Check player has the item in inventory
        var inventoryItem = await ctx.InventoryItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == rareItemType, cancellationToken);
        if (inventoryItem == null || inventoryItem.Quantity < 1)
            return (false, "Não tens esta peça rara no inventário.");

        // Check the character doesn't already have this slot applied
        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado.");

        if (character.IsRareSetSlotApplied(slot.Value))
            return (false, "Esta peça rara já foi aplicada.");

        // Consume the item from inventory
        inventoryItem.ConsumeQuantity(1);

        // Apply the rare set upgrade
        character.ApplyRareSetSlot(slot.Value);

        await ctx.SaveChangesAsync(cancellationToken);

        var slotName = EquipmentDropHelper.GetDisplayName(slot.Value);
        return (true, $"Peça rara {slotName} aplicada com sucesso!");
    }
}
