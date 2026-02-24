using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// In-game shop service — FITAB→Leitão exchange and custom skin purchase.
/// </summary>
public class ShopService : IShopService
{
    private const int FitabPerLeitao = 25;
    private const decimal CustomSkinCostFidelis = 1_000_000m;

    private readonly ICharacterRepository _characterRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        ICharacterRepository characterRepository,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IWebHostEnvironment environment,
        ILogger<ShopService> logger)
    {
        _characterRepository = characterRepository;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _contextFactory = contextFactory;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, int NewFitabBalance)> ExchangeFitabForLeitaoAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Utilizador inválido.", 0);

        // Use a fresh context to avoid stale ConcurrencyStamp issues (same pattern as BossModeService)
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await ctx.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            return (false, "Utilizador não encontrado.", 0);

        if (user.FitabBalance < FitabPerLeitao)
            return (false, $"FITAB insuficiente. Precisas de {FitabPerLeitao} FITAB (tens {user.FitabBalance}).", user.FitabBalance);

        user.FitabBalance -= FitabPerLeitao;
        await ctx.SaveChangesAsync(cancellationToken);

        // Add 1 Leitão to inventory
        await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Leitao, 1, cancellationToken);

        _logger.LogInformation("User {UserId} exchanged {Fitab} FITAB for 1 Leitão. New FITAB balance: {Balance}",
            userId, FitabPerLeitao, user.FitabBalance);

        return (true, "Trocaste 25 FITAB por 1 Leitão! 🐷", user.FitabBalance);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> PurchaseCustomSkinAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Utilizador inválido.");

        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return (false, "Personagem não encontrada.");

        if (!string.IsNullOrEmpty(character.CustomSpritePath))
            return (false, "Já tens um skin personalizado!");

        // Find the custom sprite
        var spritePath = FindCustomSprite(userId);
        if (spritePath == null)
            return (false, "Não existe sprite personalizado para a tua conta.");

        // Deduct Fidelis (fresh context for safety)
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await ctx.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            return (false, "Utilizador não encontrado.");

        if (user.FidelisBalance < CustomSkinCostFidelis)
            return (false, $"Fidelis insuficiente. Precisas de 1M Fidelis (tens {user.FidelisBalance:N0}).");

        user.FidelisBalance -= CustomSkinCostFidelis;
        await ctx.SaveChangesAsync(cancellationToken);

        // Save the custom sprite path on the character
        character.CustomSpritePath = spritePath;
        await _characterRepository.UpdateAsync(character);

        _logger.LogInformation("User {UserId} purchased custom skin: {SpritePath}. Fidelis deducted: {Cost}",
            userId, spritePath, CustomSkinCostFidelis);

        return (true, "Skin personalizado comprado com sucesso! 🎨");
    }

    /// <inheritdoc />
    public Task<bool> HasCustomSpriteAvailableAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FindCustomSprite(userId) != null);
    }

    /// <summary>
    /// Searches the arena sprites folder for boss_{username}.png (case-insensitive).
    /// Returns the web-relative path (e.g. "/sprites/games/my-tuno/enemies/arena/boss_malelo.png")
    /// or null if not found.
    /// </summary>
    private string? FindCustomSprite(string userId)
    {
        try
        {
            var arenaDir = Path.Combine(_environment.WebRootPath, "sprites", "games", "my-tuno", "enemies", "arena");
            if (!Directory.Exists(arenaDir))
                return null;

            // We need the username to build the expected filename
            // Use synchronous lookup since we're inside the service
            var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();
            if (user == null || string.IsNullOrEmpty(user.UserName))
                return null;

            var expectedFileName = $"boss_{user.UserName}.png";

            // Case-insensitive file search
            var match = Directory.GetFiles(arenaDir, "boss_*.png")
                .FirstOrDefault(f => Path.GetFileName(f).Equals(expectedFileName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return null;

            // Return web-relative path using the actual filename on disk
            return $"/sprites/games/my-tuno/enemies/arena/{Path.GetFileName(match)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for custom sprite for user {UserId}", userId);
            return null;
        }
    }
}
