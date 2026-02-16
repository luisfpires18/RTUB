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
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PowerService>? _logger;
    private readonly MyTunoScalingConfiguration _config;

    private const int MaxRetryAttempts = 3;

    public PowerService(
        ICharacterService characterService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IOptions<MyTunoScalingConfiguration> config,
        ILogger<PowerService>? logger = null)
    {
        _characterService = characterService;
        _userManager = userManager;
        _context = context;
        _logger = logger;
        _config = config.Value;
    }

    /// <summary>
    /// Calculates the cost for upgrading a specific power.
    /// Formula: Cost = BaseCost * (1 + UpgradeCount) ^ CostExponent
    /// </summary>
    public async Task<decimal> GetPowerCostAsync(string userId, PowerType powerType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var character = await _characterService.GetCharacterAsync(userId, cancellationToken);
        var upgradeCount = character != null ? GetUpgradeCount(character, powerType) : 0;
        var stat = GetPowerStatConfig(powerType);

        var cost = stat.BaseCost * (decimal)Math.Pow(1 + upgradeCount, stat.CostExponent);
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
            var cost = stat.BaseCost * (decimal)Math.Pow(1 + upgradeCount, stat.CostExponent);
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

                    var stat = GetPowerStatConfig(powerType);
                    var currentCount = GetUpgradeCount(character, powerType);

                    if (stat.MaxUpgrades > 0 && currentCount >= stat.MaxUpgrades)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Nível máximo de poder alcançado ({stat.MaxUpgrades}).");
                    }

                    var cost = stat.BaseCost * (decimal)Math.Pow(1 + currentCount, stat.CostExponent);
                    cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

                    if (user.FidelisBalance < cost)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");
                    }

                    user.FidelisBalance -= cost;
                    ApplyUpgrade(character, powerType);

                    await _userManager.UpdateAsync(user);
                    await _characterService.UpdateCharacterAsync(character, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
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

    private MyTunoUpgradeStat GetPowerStatConfig(PowerType type) => type switch
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

            var cost = stat.BaseCost * (decimal)Math.Pow(1 + currentCount, stat.CostExponent);
            cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

            if (user.FidelisBalance < cost)
                return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");

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
