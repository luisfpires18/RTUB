using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Character operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface ICharacterService
{
    /// <summary>
    /// Gets or creates a character for a user
    /// Creates a new character if one doesn't exist for the user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The character (existing or newly created)</returns>
    Task<Character> GetOrCreateCharacterAsync(string userId);

    /// <summary>
    /// Gets a character by user ID
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The character if found, otherwise null</returns>
    Task<Character?> GetCharacterAsync(string userId);

    /// <summary>
    /// Updates a character
    /// </summary>
    /// <param name="character">The character to update</param>
    Task UpdateCharacterAsync(Character character);

    /// <summary>
    /// Gets all characters ordered by level descending, including User data.
    /// Used for the owner All Characters overview page.
    /// </summary>
    /// <returns>List of all characters with User navigation loaded</returns>
    Task<List<Character>> GetAllCharactersOrderedByLevelAsync();
}
