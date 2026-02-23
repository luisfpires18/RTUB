using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing consumable upgrades (ranked improvements to consumable item effects).
/// Handles cost calculation and purchase of Cigarro dodge, Shot buff, Canhão timer, and Penalty upgrades.
/// </summary>
public interface IConsumableUpgradeService
{
    /// <summary>
    /// Gets the cost for upgrading a specific consumable upgrade type at the character's current rank.
    /// </summary>
    Task<decimal> GetConsumableUpgradeCostAsync(string userId, ConsumableUpgradeType upgradeType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates all consumable upgrade costs at once from a pre-loaded character.
    /// </summary>
    Dictionary<ConsumableUpgradeType, decimal> GetAllConsumableUpgradeCosts(Character character);

    /// <summary>
    /// Purchases a consumable upgrade rank with concurrency-safe transaction.
    /// </summary>
    Task<UpgradeResult> PurchaseConsumableUpgradeAsync(string userId, ConsumableUpgradeType upgradeType, CancellationToken cancellationToken = default);
}
