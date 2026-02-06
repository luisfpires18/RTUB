using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for inventory operations
/// Handles business logic for using and managing inventory items
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ILogger<InventoryService> _logger;
    private readonly GatheringConfig _gatheringConfig;

    // Beer heals 25% of total HP
    private const double BeerHealPercentage = 0.25;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ICharacterRepository characterRepository,
        ILogger<InventoryService> logger,
        IOptions<MyTunoScalingConfiguration> config)
    {
        _inventoryRepository = inventoryRepository;
        _characterRepository = characterRepository;
        _logger = logger;
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

        return (true, ShotBuffBattles, $"Shot ativado! +20% stats nas próximas {ShotBuffBattles} batalhas de arena");
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
}
