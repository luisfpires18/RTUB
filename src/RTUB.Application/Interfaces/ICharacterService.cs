using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Application.DTOs;

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
    /// Creates characters for all members who don't have one yet
    /// Used by OWNER role to initialize characters for all members
    /// </summary>
    /// <returns>Number of characters created</returns>
    Task<int> CreateCharactersForAllMembersAsync();
    
    /// <summary>
    /// Equip an instrument to a character
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="instrumentType">The instrument type to equip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if equipped successfully, false if user doesn't own the instrument</returns>
    Task<bool> EquipInstrumentAsync(int characterId, InventoryItemType instrumentType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unequip currently equipped instrument
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnequipInstrumentAsync(int characterId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get character's current stats including equipped instrument bonuses
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Character stats with equipment bonuses applied</returns>
    Task<CharacterStats> GetCharacterStatsWithEquipmentAsync(int characterId, CancellationToken cancellationToken = default);
}
