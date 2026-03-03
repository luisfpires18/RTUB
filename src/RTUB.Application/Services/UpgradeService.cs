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
/// Upgrade service implementation
/// Handles stat upgrade purchases with concurrency-safe Fidelis deduction
/// </summary>
public class UpgradeService : IUpgradeService
{
    private readonly ICharacterService _characterService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<UpgradeService>? _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IInventoryRepository _inventoryRepository;

    // Maximum retry attempts for concurrency conflicts
    private const int MaxRetryAttempts = 3;

    public UpgradeService(
        ICharacterService characterService,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<MyTunoScalingConfiguration> config,
        IInventoryRepository inventoryRepository,
        ILogger<UpgradeService>? logger = null)
    {
        _characterService = characterService;
        _userManager = userManager;
        _contextFactory = contextFactory;
        _logger = logger;
        _config = config.Value;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Calculates the cost for upgrading a specific stat
    /// Formula: Cost = BaseCost * (1 + UpgradeCount) ^ CostExponent
    /// </summary>
    public async Task<decimal> GetUpgradeCostAsync(string userId, StatType statType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        // Get character (don't create if it doesn't exist - just return base cost)
        var character = await _characterService.GetCharacterAsync(userId, cancellationToken);
        var upgradeCount = character != null ? statType switch
        {
            StatType.HP => character.HpUpgrades,
            StatType.Power => character.PowerUpgrades,
            StatType.Speed => character.SpeedUpgrades,
            StatType.CriticalChance => character.CriticalUpgrades,
            StatType.Defense => character.DefenseUpgrades,
            _ => 0
        } : 0;

        var upgradeStat = GetUpgradeCostConfig(statType);

        // Use quadratic or linear cost formula depending on CostScale
        var cost = CalculateUpgradeCost(upgradeCount, upgradeStat);
        return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calculates all upgrade costs at once from a pre-loaded character.
    /// Avoids 5 separate DB round-trips by reusing the same character data.
    /// </summary>
    public Dictionary<StatType, decimal> GetAllUpgradeCosts(Character character)
    {
        var statTypes = new[] { StatType.HP, StatType.Power, StatType.Speed, StatType.CriticalChance, StatType.Defense };
        var result = new Dictionary<StatType, decimal>();

        foreach (var statType in statTypes)
        {
            var upgradeCount = statType switch
            {
                StatType.HP => character.HpUpgrades,
                StatType.Power => character.PowerUpgrades,
                StatType.Speed => character.SpeedUpgrades,
                StatType.CriticalChance => character.CriticalUpgrades,
                StatType.Defense => character.DefenseUpgrades,
                _ => 0
            };

            var upgradeStat = GetUpgradeCostConfig(statType);
            var cost = CalculateUpgradeCost(upgradeCount, upgradeStat);
            result[statType] = Math.Round(cost, 2, MidpointRounding.AwayFromZero);
        }

        return result;
    }

    /// <summary>
    /// Purchases a stat upgrade with concurrency-safe transaction
    /// </summary>
    public async Task<UpgradeResult> PurchaseUpgradeAsync(string userId, StatType statType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        // Check if database supports transactions (in-memory doesn't)
        bool supportsTransactions;
        using (var providerCtx = _contextFactory.CreateDbContext())
        {
            supportsTransactions = providerCtx.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        }

        if (!supportsTransactions)
        {
            // For in-memory database (testing), use fallback method without transactions
            return await PurchaseUpgradeWithoutTransactionAsync(userId, statType, cancellationToken);
        }

        // Retry logic for concurrency conflicts (production database with transactions)
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                // Use a fresh, isolated context so the entire transaction runs on ONE connection.
                // Previous approach used _context for the transaction but _userManager/_characterService
                // wrote through different connections, causing SQLite "table locked" deadlocks.
                await using var ctx = _contextFactory.CreateDbContext();
                await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Load user and character WITH TRACKING on the same context as the transaction
                    var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                    if (user == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Utilizador não encontrado");
                    }

                    // Ensure character exists (may create on a separate context, which is fine — it's a one-time insert)
                    await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);

                    // Reload character from the transaction context to ensure we have latest upgrade counts
                    var character = await ctx.Characters
                        .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

                    if (character == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Personagem não encontrado");
                    }

                    // Calculate cost based on current upgrade count
                    var upgradeStat = GetUpgradeCostConfig(statType);

                    var currentUpgradeCount = statType switch
                    {
                        StatType.HP => character.HpUpgrades,
                        StatType.Power => character.PowerUpgrades,
                        StatType.Speed => character.SpeedUpgrades,
                        StatType.CriticalChance => character.CriticalUpgrades,
                        StatType.Defense => character.DefenseUpgrades,
                        _ => 0
                    };

                    // Check max upgrade level (0 = unlimited)
                    var maxUpgrades = upgradeStat.MaxUpgrades;
                    if (maxUpgrades > 0 && currentUpgradeCount >= maxUpgrades)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Nível máximo de upgrade alcançado ({maxUpgrades}).");
                    }

                    // Speed: also block if action time already at absolute minimum
                    if (statType == StatType.Speed && character.ActionTime <= Character.AbsoluteMinActionTime)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Velocidade já atingiu o limite mínimo.");
                    }

                    // Use quadratic or linear cost formula depending on CostScale
                    var cost = CalculateUpgradeCost(currentUpgradeCount, upgradeStat);
                    cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

                    // Validate sufficient balance
                    if (user.FidelisBalance < cost)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");
                    }

                    // Check Leitão cost (mid-game currency from Boss Mode)
                    var piggies = _config.BossMode.Piggies;
                    var leitaoCost = PiggiesCostConfig.CalculateCost(
                        currentUpgradeCount, piggies.GetStartLevelForStat(statType),
                        piggies.StatUpgradeBaseCost, piggies.StatUpgradeCostEveryNLevels);

                    if (leitaoCost > 0)
                    {
                        // Read and consume leitão on the SAME transaction context
                        var leitaoItem = await ctx.Set<InventoryItem>()
                            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == InventoryItemType.Leitao, cancellationToken);

                        if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                        {
                            await transaction.RollbackAsync();
                            return UpgradeResult.CreateFailure($"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");
                        }

                        if (!leitaoItem.ConsumeQuantity(leitaoCost))
                        {
                            await transaction.RollbackAsync();
                            return UpgradeResult.CreateFailure("Erro ao consumir Leitões");
                        }
                    }

                    // Deduct Fidelis and update concurrency stamp (replaces _userManager.UpdateAsync)
                    user.FidelisBalance -= cost;
                    user.ConcurrencyStamp = Guid.NewGuid().ToString();

                    // Apply upgrade
                    switch (statType)
                    {
                        case StatType.HP:
                            character.UpgradeHP();
                            break;
                        case StatType.Power:
                            character.UpgradePower();
                            break;
                        case StatType.Speed:
                            character.UpgradeSpeed();
                            break;
                        case StatType.CriticalChance:
                            character.UpgradeCriticalChance();
                            break;
                        case StatType.Defense:
                            character.UpgradeDefense();
                            break;
                    }

                    // Single SaveChanges persists user + character + inventory atomically on ONE connection
                    await ctx.SaveChangesAsync(cancellationToken);

                    // Commit transaction
                    await transaction.CommitAsync(cancellationToken);

                    var newUpgradeCount = statType switch
                    {
                        StatType.HP => character.HpUpgrades,
                        StatType.Power => character.PowerUpgrades,
                        StatType.Speed => character.SpeedUpgrades,
                        StatType.CriticalChance => character.CriticalUpgrades,
                        StatType.Defense => character.DefenseUpgrades,
                        _ => 0
                    };

                    return UpgradeResult.CreateSuccess(user.FidelisBalance, newUpgradeCount);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogWarning(ex, "Concurrency conflict on upgrade purchase attempt {Attempt} for user {UserId}", attempt, userId);

                    if (attempt == MaxRetryAttempts)
                    {
                        return UpgradeResult.CreateFailure("Erro de concorrência. Por favor, tente novamente.");
                    }

                    // Wait a bit before retry (exponential backoff)
                    await Task.Delay(50 * attempt, cancellationToken);
                    continue;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogError(ex, "Error purchasing upgrade for user {UserId}, stat {StatType}", userId, statType);
                    return UpgradeResult.CreateFailure($"Erro ao comprar upgrade: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in upgrade purchase transaction for user {UserId}", userId);
                if (attempt == MaxRetryAttempts)
                {
                    return UpgradeResult.CreateFailure($"Erro ao processar upgrade: {ex.Message}");
                }
            }
        }

        return UpgradeResult.CreateFailure("Falha ao processar upgrade após múltiplas tentativas");
    }

    /// <summary>
    /// Gets the upgrade cost configuration for a stat type (baseCost, costPerLevel, maxUpgrades, costScale).
    /// </summary>
    private (decimal BaseCost, decimal CostPerLevel, int MaxUpgrades, decimal CostScale) GetUpgradeCostConfig(StatType statType)
    {
        return statType switch
        {
            StatType.HP => (_config.Upgrades.HP.BaseCost, _config.Upgrades.HP.CostPerLevel, _config.Upgrades.HP.MaxUpgrades, _config.Upgrades.HP.CostScale),
            StatType.Power => (_config.Upgrades.Power.BaseCost, _config.Upgrades.Power.CostPerLevel, _config.Upgrades.Power.MaxUpgrades, _config.Upgrades.Power.CostScale),
            StatType.Speed => (_config.Upgrades.Speed.BaseCost, _config.Upgrades.Speed.CostPerLevel, _config.Upgrades.Speed.MaxUpgrades, 0),
            StatType.CriticalChance => (_config.Upgrades.CriticalChance.BaseCost, _config.Upgrades.CriticalChance.CostPerLevel, _config.Upgrades.CriticalChance.MaxUpgrades, 0),
            StatType.Defense => (_config.Upgrades.Defense.BaseCost, _config.Upgrades.Defense.CostPerLevel, _config.Upgrades.Defense.MaxUpgrades, _config.Upgrades.Defense.CostScale),
            _ => throw new ArgumentException($"Unknown stat type: {statType}", nameof(statType))
        };
    }

    /// <summary>
    /// Calculates the Fidelis cost for a stat upgrade at the given level.
    /// Uses quadratic formula when CostScale > 0, otherwise linear.
    /// </summary>
    private static decimal CalculateUpgradeCost(int currentLevel, (decimal BaseCost, decimal CostPerLevel, int MaxUpgrades, decimal CostScale) config)
    {
        if (config.CostScale > 0)
            return config.BaseCost + (decimal)((long)currentLevel * currentLevel) * config.CostScale;
        return config.BaseCost + currentLevel * config.CostPerLevel;
    }

    /// <summary>
    /// Fallback method for in-memory database that doesn't support transactions
    /// </summary>
    private async Task<UpgradeResult> PurchaseUpgradeWithoutTransactionAsync(string userId, StatType statType, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return UpgradeResult.CreateFailure("Utilizador não encontrado");
            }

            var character = await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);
            if (character == null)
            {
                return UpgradeResult.CreateFailure("Personagem não encontrado");
            }

            var upgradeStat = GetUpgradeCostConfig(statType);

            var currentUpgradeCount = statType switch
            {
                StatType.HP => character.HpUpgrades,
                StatType.Power => character.PowerUpgrades,
                StatType.Speed => character.SpeedUpgrades,
                StatType.CriticalChance => character.CriticalUpgrades,
                StatType.Defense => character.DefenseUpgrades,
                _ => 0
            };

            // Check max upgrade level (0 = unlimited)
            var maxUpgrades = upgradeStat.MaxUpgrades;
            if (maxUpgrades > 0 && currentUpgradeCount >= maxUpgrades)
            {
                return UpgradeResult.CreateFailure($"Nível máximo de upgrade alcançado ({maxUpgrades}).");
            }

            // Speed: also block if action time already at absolute minimum
            if (statType == StatType.Speed && character.ActionTime <= Character.AbsoluteMinActionTime)
            {
                return UpgradeResult.CreateFailure("Velocidade já atingiu o limite mínimo.");
            }

            // Use quadratic or linear cost formula depending on CostScale
            var cost = CalculateUpgradeCost(currentUpgradeCount, upgradeStat);
            cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

            if (user.FidelisBalance < cost)
            {
                return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");
            }

            // Check Leitão cost (mid-game currency from Boss Mode)
            var piggies = _config.BossMode.Piggies;
            var leitaoCost = PiggiesCostConfig.CalculateCost(
                currentUpgradeCount, piggies.GetStartLevelForStat(statType),
                piggies.StatUpgradeBaseCost, piggies.StatUpgradeCostEveryNLevels);

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

            switch (statType)
            {
                case StatType.HP:
                    character.UpgradeHP();
                    break;
                case StatType.Power:
                    character.UpgradePower();
                    break;
                case StatType.Speed:
                    character.UpgradeSpeed();
                    break;
                case StatType.CriticalChance:
                    character.UpgradeCriticalChance();
                    break;
                case StatType.Defense:
                    character.UpgradeDefense();
                    break;
            }

            await _userManager.UpdateAsync(user);
            await _characterService.UpdateCharacterAsync(character, cancellationToken);

            var newUpgradeCount = statType switch
            {
                StatType.HP => character.HpUpgrades,
                StatType.Power => character.PowerUpgrades,
                StatType.Speed => character.SpeedUpgrades,
                StatType.CriticalChance => character.CriticalUpgrades,
                StatType.Defense => character.DefenseUpgrades,
                _ => 0
            };

            return UpgradeResult.CreateSuccess(user.FidelisBalance, newUpgradeCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in fallback upgrade purchase for user {UserId}", userId);
            return UpgradeResult.CreateFailure($"Erro ao comprar upgrade: {ex.Message}");
        }
    }
}
