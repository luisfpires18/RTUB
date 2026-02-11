using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptions<MyTunoScalingConfiguration> _myTunoConfig;
    private readonly ILogger<CharacterService> _logger;

    public CharacterService(
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        IOptions<MyTunoScalingConfiguration> myTunoConfig,
        ILogger<CharacterService> logger)
    {
        _characterRepository = characterRepository;
        _userManager = userManager;
        _myTunoConfig = myTunoConfig;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a character for a user
    /// Creates a new character if one doesn't exist for the user
    /// </summary>
    public async Task<Character> GetOrCreateCharacterAsync(string userId, CancellationToken cancellationToken = default)
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
    public async Task<Character?> GetCharacterAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return await _characterRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Updates a character
    /// </summary>
    public async Task UpdateCharacterAsync(Character character, CancellationToken cancellationToken = default)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        await _characterRepository.UpdateAsync(character);
    }

    /// <summary>
    /// Gets all characters ordered by level descending, including User data.
    /// </summary>
    public async Task<List<Character>> GetAllCharactersOrderedByLevelAsync(CancellationToken cancellationToken = default)
    {
        return await _characterRepository.GetAllOrderedByLevelAsync();
    }

    /// <summary>
    /// Heals all characters to full HP (sets CurrentHP to null).
    /// Owner-only operation for immediate full heal.
    /// </summary>
    public async Task<int> HealAllCharactersAsync(CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
    public decimal GetDailyRewardAmount(int characterLevel)
    {
        var config = _myTunoConfig.Value.DailyReward;
        return config.BaseFidelis + (characterLevel * config.PerLevelFidelis);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, decimal RewardAmount)> ClaimDailyRewardAsync(
        string userId, int characterLevel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        try
        {
            // Re-check from DB to prevent double-claim
            var freshUser = await _userManager.FindByIdAsync(userId);
            if (freshUser == null)
                return (false, "Utilizador não encontrado.", 0);

            if (freshUser.LastDailyRewardClaim != null &&
                freshUser.LastDailyRewardClaim.Value.Date >= DateTime.UtcNow.Date)
            {
                return (false, "Já recebeste o Daily Reward hoje!", 0);
            }

            var reward = GetDailyRewardAmount(characterLevel);
            freshUser.FidelisBalance += reward;
            freshUser.LastDailyRewardClaim = DateTime.UtcNow;
            await _userManager.UpdateAsync(freshUser);

            return (true, $"Daily Reward: +{reward:F2} Fidelis!", reward);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming daily reward for user {UserId}", userId);
            return (false, "Erro ao receber Daily Reward.", 0);
        }
    }

}
