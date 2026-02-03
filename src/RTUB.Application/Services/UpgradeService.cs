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
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UpgradeService>? _logger;
    private readonly MyTunoScalingConfiguration _config;

    // Maximum retry attempts for concurrency conflicts
    private const int MaxRetryAttempts = 3;

    public UpgradeService(
        ICharacterService characterService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IOptions<MyTunoScalingConfiguration> config,
        ILogger<UpgradeService>? logger = null)
    {
        _characterService = characterService;
        _userManager = userManager;
        _context = context;
        _logger = logger;
        _config = config.Value;
    }

    /// <summary>
    /// Calculates the cost for upgrading a specific stat
    /// Formula: Cost = BaseCost * (1 + UpgradeCount) ^ 1.5
    /// </summary>
    public async Task<decimal> GetUpgradeCostAsync(string userId, StatType statType)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        // Get character (don't create if it doesn't exist - just return base cost)
        var character = await _characterService.GetCharacterAsync(userId);
        var upgradeCount = character != null ? statType switch
        {
            StatType.HP => character.HpUpgrades,
            StatType.Power => character.PowerUpgrades,
            StatType.Speed => character.SpeedUpgrades,
            StatType.CriticalChance => character.CriticalUpgrades,
            _ => 0
        } : 0;

        var baseCost = statType switch
        {
            StatType.HP => _config.Upgrades.HP.BaseCost,
            StatType.Power => _config.Upgrades.Power.BaseCost,
            StatType.Speed => _config.Upgrades.Speed.BaseCost,
            StatType.CriticalChance => _config.Upgrades.CriticalChance.BaseCost,
            _ => throw new ArgumentException($"Unknown stat type: {statType}", nameof(statType))
        };

        // Formula: Cost = BaseCost * (1 + UpgradeCount) ^ 1.5
        var cost = baseCost * (decimal)Math.Pow(1 + upgradeCount, 1.5);
        return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Purchases a stat upgrade with concurrency-safe transaction
    /// </summary>
    public async Task<UpgradeResult> PurchaseUpgradeAsync(string userId, StatType statType)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        // Check if database supports transactions (in-memory doesn't)
        var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

        if (!supportsTransactions)
        {
            // For in-memory database (testing), use fallback method without transactions
            return await PurchaseUpgradeWithoutTransactionAsync(userId, statType);
        }

        // Retry logic for concurrency conflicts (production database with transactions)
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                // Use database transaction for atomicity
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Load user and character with tracking (within transaction)
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Utilizador não encontrado");
                    }

                    // Get or create character (reload within transaction to ensure latest data)
                    var character = await _characterService.GetOrCreateCharacterAsync(userId);

                    // Reload character from database within transaction to ensure we have latest upgrade counts
                    character = await _context.Characters
                        .FirstOrDefaultAsync(c => c.UserId == userId);

                    if (character == null)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure("Personagem não encontrado");
                    }

                    // Calculate cost based on current upgrade count
                    var baseCost = statType switch
                    {
                        StatType.HP => _config.Upgrades.HP.BaseCost,
                        StatType.Power => _config.Upgrades.Power.BaseCost,
                        StatType.Speed => _config.Upgrades.Speed.BaseCost,
                        StatType.CriticalChance => _config.Upgrades.CriticalChance.BaseCost,
                        _ => throw new ArgumentException($"Unknown stat type: {statType}", nameof(statType))
                    };

                    var currentUpgradeCount = statType switch
                    {
                        StatType.HP => character.HpUpgrades,
                        StatType.Power => character.PowerUpgrades,
                        StatType.Speed => character.SpeedUpgrades,
                        StatType.CriticalChance => character.CriticalUpgrades,
                        _ => 0
                    };

                    var cost = baseCost * (decimal)Math.Pow(1 + currentUpgradeCount, 1.5);
                    cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

                    // Validate sufficient balance
                    if (user.FidelisBalance < cost)
                    {
                        await transaction.RollbackAsync();
                        return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");
                    }

                    // Deduct Fidelis
                    user.FidelisBalance -= cost;

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
                    }

                    // Save changes atomically
                    await _userManager.UpdateAsync(user);
                    await _characterService.UpdateCharacterAsync(character);
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    var newUpgradeCount = statType switch
                    {
                        StatType.HP => character.HpUpgrades,
                        StatType.Power => character.PowerUpgrades,
                        StatType.Speed => character.SpeedUpgrades,
                        StatType.CriticalChance => character.CriticalUpgrades,
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
                    await Task.Delay(50 * attempt);
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
    /// Fallback method for in-memory database that doesn't support transactions
    /// </summary>
    private async Task<UpgradeResult> PurchaseUpgradeWithoutTransactionAsync(string userId, StatType statType)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return UpgradeResult.CreateFailure("Utilizador não encontrado");
            }

            var character = await _characterService.GetOrCreateCharacterAsync(userId);
            if (character == null)
            {
                return UpgradeResult.CreateFailure("Personagem não encontrado");
            }

            var baseCost = statType switch
            {
                StatType.HP => _config.Upgrades.HP.BaseCost,
                StatType.Power => _config.Upgrades.Power.BaseCost,
                StatType.Speed => _config.Upgrades.Speed.BaseCost,
                StatType.CriticalChance => _config.Upgrades.CriticalChance.BaseCost,
                _ => throw new ArgumentException($"Unknown stat type: {statType}", nameof(statType))
            };

            var currentUpgradeCount = statType switch
            {
                StatType.HP => character.HpUpgrades,
                StatType.Power => character.PowerUpgrades,
                StatType.Speed => character.SpeedUpgrades,
                StatType.CriticalChance => character.CriticalUpgrades,
                _ => 0
            };

            var cost = baseCost * (decimal)Math.Pow(1 + currentUpgradeCount, 1.5);
            cost = Math.Round(cost, 2, MidpointRounding.AwayFromZero);

            if (user.FidelisBalance < cost)
            {
                return UpgradeResult.CreateFailure($"Saldo de Fidelis insuficiente. Necessário: {cost:F2}, Disponível: {user.FidelisBalance:F2}");
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
            }

            await _userManager.UpdateAsync(user);
            await _characterService.UpdateCharacterAsync(character);
            await _context.SaveChangesAsync();

            var newUpgradeCount = statType switch
            {
                StatType.HP => character.HpUpgrades,
                StatType.Power => character.PowerUpgrades,
                StatType.Speed => character.SpeedUpgrades,
                StatType.CriticalChance => character.CriticalUpgrades,
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
