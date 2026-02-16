using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing improvements (game-wide upgrades).
/// Handles cost calculation and purchase of energy, shot buff, and fidelis upgrades.
/// </summary>
public interface IImprovementService
{
    /// <summary>
    /// Gets the cost for upgrading a specific improvement type.
    /// </summary>
    Task<decimal> GetImprovementCostAsync(string userId, ImprovementType improvementType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates all improvement costs at once from a pre-loaded character.
    /// </summary>
    Dictionary<ImprovementType, decimal> GetAllImprovementCosts(Character character);

    /// <summary>
    /// Purchases an improvement upgrade with concurrency-safe transaction.
    /// </summary>
    Task<UpgradeResult> PurchaseImprovementAsync(string userId, ImprovementType improvementType, CancellationToken cancellationToken = default);
}
