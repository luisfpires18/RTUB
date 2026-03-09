using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// In-game shop service — FITAB→Leitão exchange.
/// </summary>
public class ShopService : IShopService
{
    private const int FitabPerLeitao = 25;
    private const int InstrumentPartsPerLeitao = 2;

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

        return (true, "Trocaste 25 FITAB por 1 Leitão! 🐷", newFitab);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, int NewInstrumentPartsBalance)> ExchangeInstrumentPartsForLeitaoAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Utilizador inválido.", 0);

        var instrumentPartTypes = InstrumentTypeHelper.AllInstrumentPartTypes;
        var partItems = await _inventoryRepository.GetItemsByTypesAsync(userId, instrumentPartTypes, cancellationToken);
        var currentParts = partItems.Sum(item => item.Quantity);

        if (currentParts < InstrumentPartsPerLeitao)
            return (false, $"Peças de instrumento insuficientes. Precisas de {InstrumentPartsPerLeitao} (tens {currentParts}).", currentParts);

        var remainingToConsume = InstrumentPartsPerLeitao;
        foreach (var partItem in partItems.OrderBy(item => item.Type))
        {
            if (remainingToConsume <= 0)
                break;

            var quantityToConsume = Math.Min(partItem.Quantity, remainingToConsume);
            if (quantityToConsume <= 0)
                continue;

            var consumed = await _inventoryRepository.ConsumeItemAsync(userId, partItem.Type, quantityToConsume, cancellationToken);
            if (!consumed)
                return (false, "Erro ao consumir peças de instrumento.", currentParts);

            remainingToConsume -= quantityToConsume;
        }

        if (remainingToConsume > 0)
            return (false, "Erro ao consumir peças de instrumento.", currentParts);

        await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Leitao, 1, cancellationToken);

        var newPartsBalance = currentParts - InstrumentPartsPerLeitao;
        return (true, "Trocaste 2 peças de instrumento por 1 Leitão! 🐷", newPartsBalance);
    }
}
