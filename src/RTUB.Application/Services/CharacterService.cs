using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Character service implementation
/// Contains business logic for character operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class CharacterService : ICharacterService
{
    private readonly ICharacterRepository _characterRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CharacterService>? _logger;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStageService? _stageService;

    public CharacterService(
        ICharacterRepository characterRepository,
        ApplicationDbContext context,
        IInventoryRepository inventoryRepository,
        ILogger<CharacterService>? logger = null,
        IStageService? stageService = null)
    {
        _characterRepository = characterRepository;
        _context = context;
        _inventoryRepository = inventoryRepository;
        _logger = logger;
        _stageService = stageService;
    }

    /// <summary>
    /// Gets or creates a character for a user
    /// Creates a new character if one doesn't exist for the user
    /// </summary>
    public async Task<Character> GetOrCreateCharacterAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        // Try to get existing character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character != null)
        {
            return character;
        }

        // Create new character
        _logger?.LogInformation("Creating new character for user {UserId}", userId);
        character = Character.Create(userId);
        await _characterRepository.AddAsync(character);

        return character;
    }

    /// <summary>
    /// Gets a character by user ID
    /// </summary>
    public async Task<Character?> GetCharacterAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return await _characterRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Updates a character
    /// </summary>
    public async Task UpdateCharacterAsync(Character character)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        await _characterRepository.UpdateAsync(character);
    }

    /// <summary>
    /// Creates characters for all members who don't have one yet
    /// </summary>
    public async Task<int> CreateCharactersForAllMembersAsync()
    {
        // Get all effective members (Caloiro, Tuno, Veterano, Tunossauro)
        var memberUserIds = await _context.Users
            .Where(u => u.Categories.Contains(Core.Enums.MemberCategory.Caloiro) ||
                       u.Categories.Contains(Core.Enums.MemberCategory.Tuno) ||
                       u.Categories.Contains(Core.Enums.MemberCategory.Veterano) ||
                       u.Categories.Contains(Core.Enums.MemberCategory.Tunossauro))
            .Select(u => u.Id)
            .ToListAsync();

        // Get existing character user IDs
        var existingCharacterUserIds = await _context.Characters
            .Select(c => c.UserId)
            .ToListAsync();

        // Find members without characters
        var membersWithoutCharacters = memberUserIds
            .Where(userId => !existingCharacterUserIds.Contains(userId))
            .ToList();

        // Create characters for members without one
        var charactersCreated = 0;
        foreach (var userId in membersWithoutCharacters)
        {
            try
            {
                var character = Character.Create(userId);
                await _characterRepository.AddAsync(character);
                charactersCreated++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating character for user {UserId}", userId);
            }
        }

        _logger?.LogInformation("Created {Count} characters for members", charactersCreated);
        return charactersCreated;
    }

    /// <summary>
    /// Equip an instrument to a character
    /// </summary>
    public async Task<bool> EquipInstrumentAsync(int characterId, InventoryItemType instrumentType, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new EntityNotFoundException(nameof(Character), characterId);
        
        // Verify user owns the instrument
        var instrument = await _inventoryRepository.GetItemAsync(character.UserId, instrumentType, cancellationToken);
        if (instrument == null || instrument.Quantity <= 0)
        {
            _logger?.LogWarning("User {UserId} does not own instrument {Instrument}", character.UserId, instrumentType);
            return false;
        }
        
        // Equip the instrument
        character.EquipInstrument(instrumentType);
        await _characterRepository.UpdateAsync(character);
        
        _logger?.LogInformation("Character {CharacterId} equipped {Instrument}", characterId, instrumentType);
        return true;
    }

    /// <summary>
    /// Unequip currently equipped instrument
    /// </summary>
    public async Task UnequipInstrumentAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new EntityNotFoundException(nameof(Character), characterId);
        
        character.EquipInstrument(null);
        await _characterRepository.UpdateAsync(character);
        
        _logger?.LogInformation("Character {CharacterId} unequipped instrument", characterId);
    }

    /// <summary>
    /// Get character's current stats including equipped instrument bonuses
    /// </summary>
    public async Task<CharacterStats> GetCharacterStatsWithEquipmentAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new EntityNotFoundException(nameof(Character), characterId);
        
        var stats = new CharacterStats
        {
            HP = character.TotalHP,
            Power = character.TotalPower,
            Speed = character.TotalSpeed,
            CriticalChance = character.TotalCriticalChance
        };
        
        // Add instrument bonuses if equipped
        if (character.EquippedInstrument.HasValue && _stageService != null)
        {
            var instrumentStats = _stageService.GetInstrumentStats(character.EquippedInstrument.Value);
            if (instrumentStats != null)
            {
                stats.HP += instrumentStats.HpBonus;
                stats.Power += instrumentStats.PowerBonus;
                stats.Speed += instrumentStats.SpeedBonus;
                stats.CriticalChance += instrumentStats.CriticalChanceBonus;
            }
        }
        
        return stats;
    }
}
