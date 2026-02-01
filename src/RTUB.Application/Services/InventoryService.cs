using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
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
            _logger.LogInformation("User {UserId} attempted to use beer but has none in inventory", userId);
            return (false, 0, "Não tens cervejas no inventário");
        }

        // Get user's character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            _logger.LogWarning("User {UserId} attempted to use beer but has no character", userId);
            return (false, 0, "Personagem não encontrado");
        }

        // Check if character needs healing
        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp >= character.TotalHP)
        {
            _logger.LogInformation("User {UserId} attempted to use beer but character is already at full HP", userId);
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

        _logger.LogInformation(
            "User {UserId} used beer to heal character {CharacterId} for {HealAmount} HP. New HP: {NewHP}/{TotalHP}",
            userId, character.Id, healAmount, character.CurrentHP, character.TotalHP);

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
    /// Use a Shot item to restore 100% HP
    /// </summary>
    public async Task<(bool Success, int HealedAmount, string Message)> UseShotAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if user has shot
            var shotItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Shot, cancellationToken);
            if (shotItem == null || shotItem.Quantity <= 0)
            {
                _logger.LogInformation("User {UserId} has no shot items", userId);
                return (false, 0, "Não tens Shot disponível");
            }
            
            // Get character
            var character = await _characterRepository.GetByUserIdAsync(userId);
            if (character == null)
            {
                _logger.LogWarning("Character not found for user {UserId}", userId);
                return (false, 0, "Personagem não encontrado");
            }
            
            // Check if character needs healing
            var currentHp = character.CurrentHP ?? character.TotalHP;
            if (currentHp >= character.TotalHP)
            {
                _logger.LogInformation("User {UserId} attempted to use shot but character is already at full HP", userId);
                return (false, 0, "HP já está cheio");
            }
            
            // Calculate heal amount (100% = full HP)
            var healAmount = character.TotalHP - currentHp;
            
            // Restore HP to full
            character.RestoreHP();
            await _characterRepository.UpdateAsync(character);
            
            // Consume shot
            var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Shot, 1, cancellationToken);
            if (!consumed)
            {
                _logger.LogError("Failed to consume shot for user {UserId}", userId);
                return (false, 0, "Erro ao usar Shot");
            }
            
            _logger.LogInformation("User {UserId} used shot to restore {HealAmount} HP. New HP: {NewHP}/{TotalHP}",
                userId, healAmount, character.CurrentHP, character.TotalHP);
            return (true, healAmount, $"HP restaurado completamente! (+{healAmount} HP)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error using shot for user {UserId}", userId);
            return (false, 0, "Erro ao usar Shot");
        }
    }
}
