using System.Threading;
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
    Task<Character> GetOrCreateCharacterAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a character by user ID
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The character if found, otherwise null</returns>
    Task<Character?> GetCharacterAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a character
    /// </summary>
    /// <param name="character">The character to update</param>
    Task UpdateCharacterAsync(Character character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all characters ordered by level descending, including User data.
    /// Used for the owner All Characters overview page.
    /// </summary>
    /// <returns>List of all characters with User navigation loaded</returns>
    Task<List<Character>> GetAllCharactersOrderedByLevelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the daily reward amount based on character level and current balance.
    /// </summary>
    /// <param name="characterLevel">The character's current level</param>
    /// <param name="currentBalance">The user's current Fidelis balance (for percentage bonus)</param>
    /// <returns>The reward amount in Fidelis</returns>
    decimal GetDailyRewardAmount(int characterLevel, decimal currentBalance = 0m);

    /// <summary>
    /// Claims the daily reward for a user. Re-checks from DB to prevent double-claim.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="characterLevel">The character's current level (for reward calculation)</param>
    /// <returns>Tuple of (Success, Message, RewardAmount)</returns>
    Task<(bool Success, string Message, decimal RewardAmount)> ClaimDailyRewardAsync(
        string userId, int characterLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a character and ALL associated game entities for the given user.
    /// </summary>
    Task<(bool Success, string Message)> DeleteCharacterAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets ALL game data for ALL users (owner operation).
    /// Deletes all characters, inventories, weapons, stage/boss/survive progress, and scores.
    /// </summary>
    Task<(bool Success, string Message)> ResetAllGameDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically assigns CustomSpritePath if a matching boss_{username}.png sprite exists
    /// and the character doesn't already have one set.
    /// </summary>
    Task AutoAssignCustomSpriteAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets stage progress and boss mode progress dictionaries for all users.
    /// Used by the All Characters overview page.
    /// </summary>
    Task<(Dictionary<string, int> StageMap, Dictionary<string, int> BossMap)> GetStageAndBossProgressAsync(CancellationToken cancellationToken = default);
}
