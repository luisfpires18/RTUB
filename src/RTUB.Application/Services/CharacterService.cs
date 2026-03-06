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
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IWebHostEnvironment _environment;

    public CharacterService(
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        IOptions<MyTunoScalingConfiguration> myTunoConfig,
        ILogger<CharacterService> logger,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IWebHostEnvironment environment)
    {
        _characterRepository = characterRepository;
        _userManager = userManager;
        _myTunoConfig = myTunoConfig;
        _logger = logger;
        _contextFactory = contextFactory;
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

    /// <inheritdoc />
    public decimal GetDailyRewardAmount(int characterLevel, decimal currentBalance = 0m)
    {
        var config = _myTunoConfig.Value.DailyReward;

        // Power curve: Multiplier × level^Exponent, floored at BaseFidelis
        var powerReward = characterLevel > 0
            ? Math.Round((decimal)(config.Multiplier * Math.Pow(characterLevel, config.Exponent)), 0)
            : 0m;

        var reward = Math.Max(config.BaseFidelis, powerReward + (characterLevel * config.PerLevelFidelis));
        var balanceBonus = Math.Round(currentBalance * config.BalancePercent, 2);
        return reward + balanceBonus;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, decimal RewardAmount)> ClaimDailyRewardAsync(
        string userId, int characterLevel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        try
        {
            // Use fresh DbContext to avoid stale ConcurrencyStamp
            // from the long-lived Blazor DbContext tracked entity.
            using var ctx = _contextFactory.CreateDbContext();
            var freshUser = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (freshUser == null)
                return (false, "Utilizador não encontrado.", 0);

            // Check daily reward claim on character (MyTuno-specific state)
            var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId);
            if (character == null)
                return (false, "Personagem não encontrada.", 0);

            if (character.LastDailyRewardClaim != null &&
                character.LastDailyRewardClaim.Value.Date >= DateTime.UtcNow.Date)
            {
                return (false, "Já recebeste o Daily Reward hoje!", 0);
            }

            var reward = GetDailyRewardAmount(characterLevel, freshUser.FidelisBalance);
            freshUser.FidelisBalance += reward;
            freshUser.ConcurrencyStamp = Guid.NewGuid().ToString();
            character.LastDailyRewardClaim = DateTime.UtcNow;
            await ctx.SaveChangesAsync();

            return (true, $"Daily Reward: +{reward:F2} Fidelis!", reward);
        }
        catch (Exception ex)
        {
            var userName = (await _userManager.FindByIdAsync(userId))?.UserName ?? userId;
            _logger.LogError(ex, "Error claiming daily reward for user {UserName}", userName);
            return (false, "Erro ao receber Daily Reward.", 0);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> DeleteCharacterAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var character = await context.Characters
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            if (character == null)
                return (false, "Personagem não encontrado.");

            // Delete all associated game entities for this user (bulk server-side deletes)
            await context.ForgedWeapons
                .Where(w => w.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await context.InventoryItems
                .Where(i => i.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await context.StageProgresses
                .Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await context.BossModeProgresses
                .Where(b => b.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await context.SurviveModeProgresses
                .Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            // NOTE: GameScores are NOT deleted here — they belong to other games
            // (bebe-mais-rui, passaro-maluco, avoid-questions, tomato-thrower)

            context.Characters.Remove(character);

            await context.SaveChangesAsync(cancellationToken);

            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogInformation("Owner deleted character and all game data for user {UserName}", userName);
            return (true, "Personagem e dados de jogo eliminados com sucesso.");
        }
        catch (Exception ex)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogError(ex, "Error deleting character for user {UserName}", userName);
            return (false, "Erro ao eliminar personagem.");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ResetAllGameDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            // Delete all game data across all users in dependency order (bulk server-side deletes)
            await context.ForgedWeapons.ExecuteDeleteAsync(cancellationToken);
            await context.InventoryItems.ExecuteDeleteAsync(cancellationToken);
            await context.StageEnemies.ExecuteDeleteAsync(cancellationToken);
            await context.StageProgresses.ExecuteDeleteAsync(cancellationToken);
            await context.BossModeProgresses.ExecuteDeleteAsync(cancellationToken);
            await context.SurviveModeProgresses.ExecuteDeleteAsync(cancellationToken);

            // NOTE: GameScores are NOT deleted here — they belong to other games
            // (bebe-mais-rui, passaro-maluco, avoid-questions, tomato-thrower)

            var count = await context.Characters.CountAsync(cancellationToken);
            await context.Characters.ExecuteDeleteAsync(cancellationToken);

            // Reset FidelisBalance for ALL users (Fidelis is cross-cutting, stays on user)
            // FITAB was already deleted as part of InventoryItems above
            await context.Users
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.FidelisBalance, 0m), cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Owner reset ALL game data: {Count} characters, all related entities deleted, and all FidelisBalance reset to 0", count);
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

        var user = await _userManager.FindByIdAsync(userId);
        var userName = user?.UserName ?? userId;
        _logger.LogInformation("Auto-assigned custom sprite for user {UserName}: {SpritePath}", userName, spritePath);
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
            var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();
            var userName = user?.UserName ?? userId;
            _logger.LogWarning(ex, "Error searching for custom sprite for user {UserName}", userName);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(Dictionary<string, int> StageMap, Dictionary<string, int> BossMap)> GetStageAndBossProgressAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var stageTask = context.StageProgresses
            .AsNoTracking()
            .ToDictionaryAsync(s => s.UserId, s => s.HighestStage, cancellationToken);
        var bossTask = context.BossModeProgresses
            .AsNoTracking()
            .ToDictionaryAsync(b => b.UserId, b => b.HighestBossStage, cancellationToken);
        await Task.WhenAll(stageTask, bossTask);
        return (await stageTask, await bossTask);
    }
}
