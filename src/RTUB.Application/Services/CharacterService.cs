using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
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
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public CharacterService(
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        IOptions<MyTunoScalingConfiguration> myTunoConfig,
        ILogger<CharacterService> logger,
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment)
    {
        _characterRepository = characterRepository;
        _userManager = userManager;
        _myTunoConfig = myTunoConfig;
        _logger = logger;
        _dbContext = dbContext;
        _environment = environment;
    }

    /// <summary>
    /// Gets or creates a character for a user
    /// Creates a new character if one doesn't exist for the user
    /// </summary>
    public async Task<Character> GetOrCreateCharacterAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        // Try to get existing character — use Fresh variant to pick up
        // external DB changes from another circuit.
        var character = await _characterRepository.GetByUserIdFreshAsync(userId);
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

    /// <inheritdoc />
    public decimal GetDailyRewardAmount(int characterLevel, decimal currentBalance = 0m)
    {
        var config = _myTunoConfig.Value.DailyReward;
        var baseReward = config.BaseFidelis + (characterLevel * config.PerLevelFidelis);
        var balanceBonus = Math.Round(currentBalance * config.BalancePercent, 2);
        return baseReward + balanceBonus;
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

            var reward = GetDailyRewardAmount(characterLevel, freshUser.FidelisBalance);
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

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> DeleteCharacterAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var character = await _dbContext.Characters
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            if (character == null)
                return (false, "Personagem não encontrado.");

            // Delete all associated game entities for this user (bulk server-side deletes)
            await _dbContext.ForgedWeapons
                .Where(w => w.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await _dbContext.InventoryItems
                .Where(i => i.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await _dbContext.StageProgresses
                .Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await _dbContext.BossModeProgresses
                .Where(b => b.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await _dbContext.SurviveModeProgresses
                .Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            // NOTE: GameScores are NOT deleted here — they belong to other games
            // (bebe-mais-rui, passaro-maluco, avoid-questions, tomato-thrower)

            _dbContext.Characters.Remove(character);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Owner deleted character and all game data for user {UserId}", userId);
            return (true, "Personagem e dados de jogo eliminados com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting character for user {UserId}", userId);
            return (false, "Erro ao eliminar personagem.");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ResetAllGameDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete all game data across all users in dependency order (bulk server-side deletes)
            await _dbContext.ForgedWeapons.ExecuteDeleteAsync(cancellationToken);
            await _dbContext.InventoryItems.ExecuteDeleteAsync(cancellationToken);
            await _dbContext.StageEnemies.ExecuteDeleteAsync(cancellationToken);
            await _dbContext.StageProgresses.ExecuteDeleteAsync(cancellationToken);
            await _dbContext.BossModeProgresses.ExecuteDeleteAsync(cancellationToken);
            await _dbContext.SurviveModeProgresses.ExecuteDeleteAsync(cancellationToken);

            // NOTE: GameScores are NOT deleted here — they belong to other games
            // (bebe-mais-rui, passaro-maluco, avoid-questions, tomato-thrower)

            var count = await _dbContext.Characters.CountAsync(cancellationToken);
            await _dbContext.Characters.ExecuteDeleteAsync(cancellationToken);

            // Reset FidelisBalance and FitabBalance to 0 for ALL users
            await _dbContext.Users
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.FidelisBalance, 0m)
                    .SetProperty(x => x.FitabBalance, 5), cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Owner reset ALL game data: {Count} characters, all related entities deleted, and all FidelisBalance/FitabBalance reset to 0", count);
            return (true, $"Todos os dados de jogo foram resetados. {count} personagens eliminados.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting all game data");
            return (false, "Erro ao resetar dados de jogo.");
        }
    }

    /// <inheritdoc />
    public async Task AutoAssignCustomSpriteAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null || !string.IsNullOrEmpty(character.CustomSpritePath)) return;

        var spritePath = FindCustomSprite(userId);
        if (spritePath == null) return;

        character.CustomSpritePath = spritePath;
        await _characterRepository.UpdateAsync(character);

        _logger.LogInformation("Auto-assigned custom sprite for user {UserId}: {SpritePath}", userId, spritePath);
    }

    /// <summary>
    /// Searches the arena sprites folder for boss_{username}.png (case-insensitive).
    /// </summary>
    private string? FindCustomSprite(string userId)
    {
        try
        {
            var arenaDir = Path.Combine(_environment.WebRootPath, "sprites", "games", "my-tuno", "enemies", "arena");
            if (!Directory.Exists(arenaDir))
                return null;

            var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();
            if (user == null || string.IsNullOrEmpty(user.UserName))
                return null;

            var expectedFileName = $"boss_{user.UserName}.png";
            var match = Directory.GetFiles(arenaDir, "boss_*.png")
                .FirstOrDefault(f => Path.GetFileName(f).Equals(expectedFileName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return null;

            return $"/sprites/games/my-tuno/enemies/arena/{Path.GetFileName(match)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for custom sprite for user {UserId}", userId);
            return null;
        }
    }
}
