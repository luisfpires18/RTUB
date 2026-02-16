using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing powers (combat power enhancements).
/// Handles cost calculation and purchase of heavy attack and special attack upgrades.
/// </summary>
public interface IPowerService
{
    /// <summary>
    /// Gets the cost for upgrading a specific power type.
    /// </summary>
    Task<decimal> GetPowerCostAsync(string userId, PowerType powerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates all power costs at once from a pre-loaded character.
    /// </summary>
    Dictionary<PowerType, decimal> GetAllPowerCosts(Character character);

    /// <summary>
    /// Purchases a power upgrade with concurrency-safe transaction.
    /// </summary>
    Task<UpgradeResult> PurchasePowerAsync(string userId, PowerType powerType, CancellationToken cancellationToken = default);
}
