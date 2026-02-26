using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// In-game shop service — FITAB→Leitão exchange.
/// </summary>
public class ShopService : IShopService
{
    private const int FitabPerLeitao = 25;

    private readonly IInventoryRepository _inventoryRepository;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        IInventoryRepository inventoryRepository,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<ShopService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, int NewFitabBalance)> ExchangeFitabForLeitaoAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Utilizador inválido.", 0);

        // Use a fresh context for atomic FITAB deduction + Leitão addition
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await ctx.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            return (false, "Utilizador não encontrado.", 0);

        if (user.FitabBalance < FitabPerLeitao)
            return (false, $"FITAB insuficiente. Precisas de {FitabPerLeitao} FITAB (tens {user.FitabBalance}).", user.FitabBalance);

        user.FitabBalance -= FitabPerLeitao;

        // Add 1 Leitão to inventory on the same context (atomic with FITAB deduction)
        var leitaoItem = await ctx.InventoryItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == InventoryItemType.Leitao, cancellationToken);
        if (leitaoItem != null)
            leitaoItem.AddQuantity(1);
        else
            await ctx.InventoryItems.AddAsync(InventoryItem.Create(userId, InventoryItemType.Leitao, 1), cancellationToken);

        await ctx.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} exchanged {Fitab} FITAB for 1 Leitão. New FITAB balance: {Balance}",
            userId, FitabPerLeitao, user.FitabBalance);

        return (true, "Trocaste 25 FITAB por 1 Leitão! 🐷", user.FitabBalance);
    }
}
