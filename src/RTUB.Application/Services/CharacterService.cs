using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

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

    public CharacterService(
        ICharacterRepository characterRepository,
        ApplicationDbContext context,
        ILogger<CharacterService>? logger = null)
    {
        _characterRepository = characterRepository;
        _context = context;
        _logger = logger;
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
}
