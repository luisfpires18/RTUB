using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// In-game shop service — FITAB→Leitão exchange.
/// </summary>
public class ShopService : IShopService
{
    private const int FitabPerLeitao = 25;

    private readonly IInventoryRepository _inventoryRepository;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        IInventoryRepository inventoryRepository,
        ILogger<ShopService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, int NewFitabBalance)> ExchangeFitabForLeitaoAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Utilizador inválido.", 0);

        // Check FITAB balance from inventory
        var fitabItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Fitab, cancellationToken);
        var currentFitab = fitabItem?.Quantity ?? 0;

        if (currentFitab < FitabPerLeitao)
            return (false, $"FITAB insuficiente. Precisas de {FitabPerLeitao} FITAB (tens {currentFitab}).", currentFitab);

        // Consume FITAB and add Leitão via inventory (atomic per-item)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Fitab, FitabPerLeitao, cancellationToken);
        if (!consumed)
            return (false, "Erro ao consumir FITAB.", currentFitab);

        await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Leitao, 1, cancellationToken);

        var newFitab = currentFitab - FitabPerLeitao;
        _logger.LogInformation("User {UserId} exchanged {Fitab} FITAB for 1 Leitão. New FITAB balance: {Balance}",
            userId, FitabPerLeitao, newFitab);

        return (true, "Trocaste 25 FITAB por 1 Leitão! 🐷", newFitab);
    }
}
