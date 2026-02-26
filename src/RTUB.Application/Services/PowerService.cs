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
/// Power service implementation.
/// Handles combat power enhancement upgrades with concurrency-safe Fidelis deduction.
/// </summary>
public class PowerService : IPowerService
{
    private readonly ICharacterService _characterService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PowerService>? _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IInventoryRepository _inventoryRepository;

    private const int MaxRetryAttempts = 3;

    public PowerService(
        ICharacterService characterService,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<MyTunoScalingConfiguration> config,
        IInventoryRepository inventoryRepository,
        ILogger<PowerService>? logger = null)
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
    /// Calculates the cost for upgrading a specific power.
    /// Formula: Cost = BaseCost + UpgradeCount * CostPerLevel
    /// </summary>
    public async Task<decimal> GetPowerCostAsync(string userId, PowerType powerType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var character = await _characterService.GetCharacterAsync(userId, cancellationToken);
        var upgradeCount = character != null ? GetUpgradeCount(character, powerType) : 0;
        var stat = GetPowerStatConfig(powerType);

        var cost = stat.BaseCost + upgradeCount * stat.CostPerLevel;
        return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calculates all power costs at once from a pre-loaded character.
    /// </summary>
    public Dictionary<PowerType, decimal> GetAllPowerCosts(Character character)
    {
        var types = new[] { PowerType.HeavyAttack, PowerType.SpecialAttack };
        var result = new Dictionary<PowerType, decimal>();

        foreach (var type in types)
        {
            var upgradeCount = GetUpgradeCount(character, type);
            var stat = GetPowerStatConfig(type);
            var cost = stat.BaseCost + upgradeCount * stat.CostPerLevel;
            result[type] = Math.Round(cost, 2, MidpointRounding.AwayFromZero);
        }

        return result;
    }

    /// <summary>
    /// Purchases a power upgrade with concurrency-safe transaction.
    /// </summary>
    public async Task<UpgradeResult> PurchasePowerAsync(string userId, PowerType powerType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

        if (!supportsTransactions)
            return await PurchaseWithoutTransactionAsync(userId, powerType, cancellationToken);

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                // Use a fresh, isolated context so the entire transaction runs on ONE connection.
                await using var ctx = _contextFactory.CreateDbContext();
                await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                    if (user == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Utilizador não encontrado");
                    }

                    await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);
                    var character = await ctx.Characters
                        .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

                    if (character == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Personagem não encontrado");
                    }

                    var stat = GetPowerStatConfig(powerType);
                    var currentCount = GetUpgradeCount(character, powerType);

                    if (stat.MaxUpgrades > 0 && currentCount >= stat.MaxUpgrades)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Nível máximo de poder alcançado ({stat.MaxUpgrades}).");
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
                        currentCount, piggies.PowerStartLevel,
                        piggies.PowerBaseCost, piggies.PowerCostEveryNLevels);

                    if (leitaoCost > 0)
                    {
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

                    user.FidelisBalance -= cost;
                    user.ConcurrencyStamp = Guid.NewGuid().ToString();
                    ApplyUpgrade(character, powerType);

                    await ctx.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return UpgradeResult.CreateSuccess(user.FidelisBalance, GetUpgradeCount(character, powerType));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogWarning(ex, "Concurrency conflict on power purchase attempt {Attempt} for user {UserId}", attempt, userId);
                    if (attempt == MaxRetryAttempts)
                        return UpgradeResult.CreateFailure("Erro de concorrência. Por favor, tente novamente.");
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger?.LogError(ex, "Error purchasing power for user {UserId}, type {PowerType}", userId, powerType);
                    return UpgradeResult.CreateFailure($"Erro ao comprar poder: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in power purchase transaction for user {UserId}", userId);
                if (attempt == MaxRetryAttempts)
                    return UpgradeResult.CreateFailure($"Erro ao processar poder: {ex.Message}");
            }
        }

        return UpgradeResult.CreateFailure("Falha ao processar poder após múltiplas tentativas");
    }

    private int GetUpgradeCount(Character character, PowerType type) => type switch
    {
        PowerType.HeavyAttack => character.HeavyAttackUpgrades,
        PowerType.SpecialAttack => character.SpecialAttackUpgrades,
        _ => 0
    };

    private void ApplyUpgrade(Character character, PowerType type)
    {
        switch (type)
        {
            case PowerType.HeavyAttack:
                character.UpgradeHeavyAttack();
                break;
            case PowerType.SpecialAttack:
                character.UpgradeSpecialAttack();
                break;
        }
    }

    private UpgradeFlatStat GetPowerStatConfig(PowerType type) => type switch
    {
        PowerType.HeavyAttack => _config.Powers.HeavyAttack,
        PowerType.SpecialAttack => _config.Powers.SpecialAttack,
        _ => throw new ArgumentException($"Unknown power type: {type}", nameof(type))
    };

    private async Task<UpgradeResult> PurchaseWithoutTransactionAsync(string userId, PowerType powerType, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return UpgradeResult.CreateFailure("Utilizador não encontrado");

            var character = await _characterService.GetOrCreateCharacterAsync(userId, cancellationToken);
            if (character == null)
                return UpgradeResult.CreateFailure("Personagem não encontrado");

            var stat = GetPowerStatConfig(powerType);
            var currentCount = GetUpgradeCount(character, powerType);

            if (stat.MaxUpgrades > 0 && currentCount >= stat.MaxUpgrades)
                return UpgradeResult.CreateFailure($"Nível máximo de poder alcançado ({stat.MaxUpgrades}).");

            var cost = stat.BaseCost + currentCount * stat.CostPerLevel;
            cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

            if (user.FidelisBalance < cost)
                return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");

            // Check Leitão cost (mid-game currency from Boss Mode)
            var piggies = _config.BossMode.Piggies;
            var leitaoCost = PiggiesCostConfig.CalculateCost(
                currentCount, piggies.PowerStartLevel,
                piggies.PowerBaseCost, piggies.PowerCostEveryNLevels);

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
            ApplyUpgrade(character, powerType);

            await _userManager.UpdateAsync(user);
            await _characterService.UpdateCharacterAsync(character, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return UpgradeResult.CreateSuccess(user.FidelisBalance, GetUpgradeCount(character, powerType));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in fallback power purchase for user {UserId}", userId);
            return UpgradeResult.CreateFailure($"Erro ao comprar poder: {ex.Message}");
        }
    }
}
