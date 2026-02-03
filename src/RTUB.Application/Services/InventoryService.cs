using Microsoft.Extensions.Logging;
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

    // Beer heals 25% of total HP
    private const double BeerHealPercentage = 0.25;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ICharacterRepository characterRepository,
        ILogger<InventoryService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _characterRepository = characterRepository;
        _logger = logger;
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

        // Check if character is dead
        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, 0, "Não podes usar cerveja num personagem morto");
        }

        // Check if character needs healing
        if (currentHp >= character.TotalHP)
        {
            return (false, 0, "O personagem já está com HP máximo");
        }

        // Calculate heal amount (25% of TotalHP, rounded)
        var healAmount = (int)Math.Round(character.TotalHP * BeerHealPercentage);

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
}
