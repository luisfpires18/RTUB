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
    public CharacterService(
        ICharacterRepository characterRepository)
    {
        _characterRepository = characterRepository;
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
    /// Gets all characters ordered by level descending, including User data.
    /// </summary>
    public async Task<List<Character>> GetAllCharactersOrderedByLevelAsync()
    {
        return await _characterRepository.GetAllOrderedByLevelAsync();
    }

    /// <summary>
    /// Heals all characters to full HP (sets CurrentHP to null).
    /// Owner-only operation for immediate full heal.
    /// </summary>
    public async Task<int> HealAllCharactersAsync()
    {
        var damagedCharacters = (await _characterRepository
            .FindAsync(c => c.CurrentHP != null))
            .ToList();

        foreach (var character in damagedCharacters)
        {
            character.CurrentHP = null; // null = full HP
        }

        if (damagedCharacters.Count > 0)
        {
            await _characterRepository.SaveChangesAsync();
        }

        return damagedCharacters.Count;
    }

}
