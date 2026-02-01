using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for character stat upgrade operations
/// Handles upgrade cost calculation and purchase with concurrency safety
/// </summary>
public interface IUpgradeService
{
    /// <summary>
    /// Calculates the cost for upgrading a specific stat
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="statType">The stat type to upgrade (HP, Power, Speed, or Critical Chance)</param>
    /// <returns>The cost in Fidelis for the next upgrade</returns>
    Task<decimal> GetUpgradeCostAsync(string userId, StatType statType);

    /// <summary>
    /// Purchases a stat upgrade for the user's character
    /// Atomically deducts Fidelis and increments upgrade count
    /// Handles concurrency conflicts with retry logic
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="statType">The stat type to upgrade (HP, Power, Speed, or Critical Chance)</param>
    /// <returns>Result indicating success or failure with updated values</returns>
    Task<UpgradeResult> PurchaseUpgradeAsync(string userId, StatType statType);
}
