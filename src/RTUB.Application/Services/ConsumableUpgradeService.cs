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
/// Consumable upgrade service implementation.
/// Handles ranked upgrades to consumable item effects with concurrency-safe Fidelis deduction.
/// </summary>
public class ConsumableUpgradeService : IConsumableUpgradeService
{
    private readonly ICharacterService _characterService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConsumableUpgradeService>? _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IInventoryRepository _inventoryRepository;

    private const int MaxRetryAttempts = 3;

    public ConsumableUpgradeService(
        ICharacterService characterService,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<MyTunoScalingConfiguration> config,
        IInventoryRepository inventoryRepository,
        ILogger<ConsumableUpgradeService>? logger = null)
    {
        _characterService = characterService;
        _userManager = userManager;
        _contextFactory = contextFactory;
        _context = contextFactory.CreateDbContext();
        _logger = logger;
        _config = config.Value;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Calculates the cost for upgrading a specific consumable at the character's current rank.
    /// </summary>
    public async Task<decimal> GetConsumableUpgradeCostAsync(string userId, ConsumableUpgradeType upgradeType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var character = await _characterService.GetCharacterAsync(userId, cancellationToken);
        var currentRank = character != null ? GetUpgradeCount(character, upgradeType) : 0;
        var tier = GetTierConfig(upgradeType);

        return CalculateCost(tier, currentRank);
    }

    /// <summary>
    /// Calculates all consumable upgrade costs at once from a pre-loaded character.
    /// </summary>
    public Dictionary<ConsumableUpgradeType, decimal> GetAllConsumableUpgradeCosts(Character character)
    {
        var result = new Dictionary<ConsumableUpgradeType, decimal>();

        foreach (var type in Enum.GetValues<ConsumableUpgradeType>())
        {
            var currentRank = GetUpgradeCount(character, type);
            var tier = GetTierConfig(type);
            result[type] = CalculateCost(tier, currentRank);
        }

        return result;
    }

    /// <summary>
    /// Purchases a consumable upgrade rank with concurrency-safe transaction.
    /// </summary>
    public async Task<UpgradeResult> PurchaseConsumableUpgradeAsync(string userId, ConsumableUpgradeType upgradeType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

        if (!supportsTransactions)
            return await PurchaseWithoutTransactionAsync(userId, upgradeType, cancellationToken);

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

                    var tier = GetTierConfig(upgradeType);
                    var currentRank = GetUpgradeCount(character, upgradeType);

                    if (currentRank >= tier.MaxUpgrades)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Nível máximo alcançado ({tier.MaxUpgrades}).");
                    }

                    var cost = CalculateCost(tier, currentRank);

                    if (user.FidelisBalance < cost)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:N0}, Disponível: {user.FidelisBalance:N0}");
                    }

                    // Check Leitão cost
                    var piggies = _config.BossMode.Piggies;
                    var leitaoCost = PiggiesCostConfig.CalculateCost(
                        currentRank, piggies.ImprovementStartLevel,
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
                    ApplyUpgrade(character, upgradeType);

                    await _userManager.UpdateAsync(user);
                    await _characterService.UpdateCharacterAsync(character, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return UpgradeResult.CreateSuccess(user.FidelisBalance, GetUpgradeCount(character, upgradeType));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogWarning(ex, "Concurrency conflict on consumable upgrade purchase attempt {Attempt} for user {UserId}", attempt, userId);
                    if (attempt == MaxRetryAttempts)
                        return UpgradeResult.CreateFailure("Erro de concorrência. Por favor, tente novamente.");
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogError(ex, "Error purchasing consumable upgrade for user {UserId}, type {Type}", userId, upgradeType);
                    return UpgradeResult.CreateFailure($"Erro ao comprar upgrade: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in consumable upgrade transaction for user {UserId}", userId);
                if (attempt == MaxRetryAttempts)
                    return UpgradeResult.CreateFailure($"Erro ao processar upgrade: {ex.Message}");
            }
        }

        return UpgradeResult.CreateFailure("Falha ao processar upgrade após múltiplas tentativas");
    }

    private int GetUpgradeCount(Character character, ConsumableUpgradeType type) => type switch
    {
        ConsumableUpgradeType.CigarroDodge => character.CigarroDodgeUpgrades,
        ConsumableUpgradeType.ShotBuff => character.ShotStatBuffUpgrades,
        ConsumableUpgradeType.CanhaoTimer => character.CanhaoTimerUpgrades,
        ConsumableUpgradeType.PenaltyTimer => character.PenaltyTimerUpgrades,
        _ => 0
    };

    private void ApplyUpgrade(Character character, ConsumableUpgradeType type)
    {
        switch (type)
        {
            case ConsumableUpgradeType.CigarroDodge:
                character.UpgradeCigarroDodge();
                break;
            case ConsumableUpgradeType.ShotBuff:
                character.UpgradeShotStatBuff();
                break;
            case ConsumableUpgradeType.CanhaoTimer:
                character.UpgradeCanhaoTimer();
                break;
            case ConsumableUpgradeType.PenaltyTimer:
                character.UpgradePenaltyTimer();
                break;
        }
    }

    private ConsumableUpgradeTier GetTierConfig(ConsumableUpgradeType type) => type switch
    {
        ConsumableUpgradeType.CigarroDodge => _config.ConsumableUpgrades.CigarroDodge,
        ConsumableUpgradeType.ShotBuff => _config.ConsumableUpgrades.ShotBuff,
        ConsumableUpgradeType.CanhaoTimer => _config.ConsumableUpgrades.CanhaoTimer,
        ConsumableUpgradeType.PenaltyTimer => _config.ConsumableUpgrades.PenaltyTimer,
        _ => throw new ArgumentException($"Unknown consumable upgrade type: {type}", nameof(type))
    };

    /// <summary>
    /// Calculates the Fidelis cost for a given consumable upgrade at a given rank.
    /// Doubling: BaseCost × 2^n (e.g. 5M, 10M, 20M, 40M, 80M)
    /// Linear: BaseCost + n × CostPerLevel (e.g. 50M, 100M, 150M)
    /// </summary>
    private static decimal CalculateCost(ConsumableUpgradeTier tier, int currentRank)
    {
        decimal cost;
        if (tier.CostFormula == CostFormulaType.Doubling)
        {
            cost = tier.BaseCost * (decimal)Math.Pow(2, currentRank);
        }
        else
        {
            cost = tier.BaseCost + currentRank * tier.CostPerLevel;
        }
        return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<UpgradeResult> PurchaseWithoutTransactionAsync(string userId, ConsumableUpgradeType upgradeType, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return UpgradeResult.CreateFailure("Utilizador não encontrado");

            var character = await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);
            if (character == null)
                return UpgradeResult.CreateFailure("Personagem não encontrado");

            var tier = GetTierConfig(upgradeType);
            var currentRank = GetUpgradeCount(character, upgradeType);

            if (currentRank >= tier.MaxUpgrades)
                return UpgradeResult.CreateFailure($"Nível máximo alcançado ({tier.MaxUpgrades}).");

            var cost = CalculateCost(tier, currentRank);

            if (user.FidelisBalance < cost)
                return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:N0}, Disponível: {user.FidelisBalance:N0}");

            // Check Leitão cost
            var piggies = _config.BossMode.Piggies;
            var leitaoCost = PiggiesCostConfig.CalculateCost(
                currentRank, piggies.ImprovementStartLevel,
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
            ApplyUpgrade(character, upgradeType);

            await _userManager.UpdateAsync(user);
            await _characterService.UpdateCharacterAsync(character, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return UpgradeResult.CreateSuccess(user.FidelisBalance, GetUpgradeCount(character, upgradeType));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in fallback consumable upgrade purchase for user {UserId}", userId);
            return UpgradeResult.CreateFailure($"Erro ao comprar upgrade: {ex.Message}");
        }
    }
}
