using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Improvement service implementation.
/// Handles game-wide improvement upgrades with concurrency-safe Fidelis deduction.
/// </summary>
public class ImprovementService : IImprovementService
{
    private readonly ICharacterService _characterService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImprovementService>? _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IInventoryRepository _inventoryRepository;

    private const int MaxRetryAttempts = 3;

    public ImprovementService(
        ICharacterService characterService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IOptions<MyTunoScalingConfiguration> config,
        IInventoryRepository inventoryRepository,
        ILogger<ImprovementService>? logger = null)
    {
        _characterService = characterService;
        _userManager = userManager;
        _context = context;
        _logger = logger;
        _config = config.Value;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Calculates the cost for upgrading a specific improvement.
    /// Formula: Cost = BaseCost + UpgradeCount * CostPerLevel
    /// </summary>
    public async Task<decimal> GetImprovementCostAsync(string userId, ImprovementType improvementType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var character = await _characterService.GetCharacterAsync(userId, cancellationToken);
        var upgradeCount = character != null ? GetUpgradeCount(character, improvementType) : 0;
        var stat = GetImprovementStatConfig(improvementType);

        var cost = stat.BaseCost + upgradeCount * stat.CostPerLevel;
        return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calculates all improvement costs at once from a pre-loaded character.
    /// </summary>
    public Dictionary<ImprovementType, decimal> GetAllImprovementCosts(Character character)
    {
        var types = new[] { ImprovementType.EnergyAmount, ImprovementType.EnergyRegen };
        var result = new Dictionary<ImprovementType, decimal>();

        foreach (var type in types)
        {
            var upgradeCount = GetUpgradeCount(character, type);
            var stat = GetImprovementStatConfig(type);
            var cost = stat.BaseCost + upgradeCount * stat.CostPerLevel;
            result[type] = Math.Round(cost, 2, MidpointRounding.AwayFromZero);
        }

        return result;
    }

    /// <summary>
    /// Purchases an improvement upgrade with concurrency-safe transaction.
    /// </summary>
    public async Task<UpgradeResult> PurchaseImprovementAsync(string userId, ImprovementType improvementType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

        if (!supportsTransactions)
            return await PurchaseWithoutTransactionAsync(userId, improvementType, cancellationToken);

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Utilizador não encontrado");
                    }

                    await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);
                    var character = await _context.Characters
                        .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

                    if (character == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Personagem não encontrado");
                    }

                    var stat = GetImprovementStatConfig(improvementType);
                    var currentCount = GetUpgradeCount(character, improvementType);

                    if (stat.MaxUpgrades > 0 && currentCount >= stat.MaxUpgrades)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Nível máximo de melhoria alcançado ({stat.MaxUpgrades}).");
                    }

                    var cost = stat.BaseCost + currentCount * stat.CostPerLevel;
                    cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

                    if (user.FidelisBalance < cost)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");
                    }

                    // Check Leitão cost (mid-game currency from Boss Mode)
                    var piggies = _config.BossMode.Piggies;
                    var leitaoCost = PiggiesCostConfig.CalculateCost(
                        currentCount, piggies.ImprovementStartLevel,
                        piggies.ImprovementBaseCost, piggies.ImprovementCostEveryNLevels);

                    if (leitaoCost > 0)
                    {
                        var leitaoItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Leitao, cancellationToken);
                        if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                        {
                            await transaction.RollbackAsync();
                            return UpgradeResult.CreateFailure($"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");
                        }

                        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Leitao, leitaoCost, cancellationToken);
                        if (!consumed)
                        {
                            await transaction.RollbackAsync();
                            return UpgradeResult.CreateFailure("Erro ao consumir Leitões");
                        }
                    }

                    user.FidelisBalance -= cost;
                    ApplyUpgrade(character, improvementType);

                    await _userManager.UpdateAsync(user);
                    await _characterService.UpdateCharacterAsync(character, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return UpgradeResult.CreateSuccess(user.FidelisBalance, GetUpgradeCount(character, improvementType));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogWarning(ex, "Concurrency conflict on improvement purchase attempt {Attempt} for user {UserId}", attempt, userId);
                    if (attempt == MaxRetryAttempts)
                        return UpgradeResult.CreateFailure("Erro de concorrência. Por favor, tente novamente.");
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogError(ex, "Error purchasing improvement for user {UserId}, type {ImprovementType}", userId, improvementType);
                    return UpgradeResult.CreateFailure($"Erro ao comprar melhoria: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in improvement purchase transaction for user {UserId}", userId);
                if (attempt == MaxRetryAttempts)
                    return UpgradeResult.CreateFailure($"Erro ao processar melhoria: {ex.Message}");
            }
        }

        return UpgradeResult.CreateFailure("Falha ao processar melhoria após múltiplas tentativas");
    }

    private int GetUpgradeCount(Character character, ImprovementType type) => type switch
    {
        ImprovementType.EnergyAmount => character.EnergyAmountUpgrades,
        ImprovementType.EnergyRegen => character.EnergyRegenUpgrades,
        _ => 0
    };

    private void ApplyUpgrade(Character character, ImprovementType type)
    {
        switch (type)
        {
            case ImprovementType.EnergyAmount:
                character.UpgradeEnergyAmount();
                break;
            case ImprovementType.EnergyRegen:
                character.UpgradeEnergyRegen();
                break;
        }
    }

    private UpgradeFlatStat GetImprovementStatConfig(ImprovementType type) => type switch
    {
        ImprovementType.EnergyAmount => _config.Improvements.EnergyAmount,
        ImprovementType.EnergyRegen => _config.Improvements.EnergyRegen,
        _ => throw new ArgumentException($"Unknown improvement type: {type}", nameof(type))
    };

    private async Task<UpgradeResult> PurchaseWithoutTransactionAsync(string userId, ImprovementType improvementType, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return UpgradeResult.CreateFailure("Utilizador não encontrado");

            var character = await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);
            if (character == null)
                return UpgradeResult.CreateFailure("Personagem não encontrado");

            var stat = GetImprovementStatConfig(improvementType);
            var currentCount = GetUpgradeCount(character, improvementType);

            if (stat.MaxUpgrades > 0 && currentCount >= stat.MaxUpgrades)
                return UpgradeResult.CreateFailure($"Nível máximo de melhoria alcançado ({stat.MaxUpgrades}).");

            var cost = stat.BaseCost + currentCount * stat.CostPerLevel;
            cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

            if (user.FidelisBalance < cost)
                return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");

            // Check Leitão cost (mid-game currency from Boss Mode)
            var piggies = _config.BossMode.Piggies;
            var leitaoCost = PiggiesCostConfig.CalculateCost(
                currentCount, piggies.ImprovementStartLevel,
                piggies.ImprovementBaseCost, piggies.ImprovementCostEveryNLevels);

            if (leitaoCost > 0)
            {
                var leitaoItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Leitao, cancellationToken);
                if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                    return UpgradeResult.CreateFailure($"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Leitao, leitaoCost, cancellationToken);
                if (!consumed)
                    return UpgradeResult.CreateFailure("Erro ao consumir Leitões");
            }

            user.FidelisBalance -= cost;
            ApplyUpgrade(character, improvementType);

            await _userManager.UpdateAsync(user);
            await _characterService.UpdateCharacterAsync(character, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return UpgradeResult.CreateSuccess(user.FidelisBalance, GetUpgradeCount(character, improvementType));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in fallback improvement purchase for user {UserId}", userId);
            return UpgradeResult.CreateFailure($"Erro ao comprar melhoria: {ex.Message}");
        }
    }
}
