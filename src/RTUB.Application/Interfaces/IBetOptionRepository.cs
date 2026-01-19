using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for BetOption entity with domain-specific operations
/// Extends generic repository with bet option-specific queries
/// </summary>
public interface IBetOptionRepository : IRepository<BetOption>
{
    /// <summary>
    /// Gets all options for a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    /// <returns>Collection of bet options</returns>
    Task<IEnumerable<BetOption>> GetByBetIdAsync(int betId);

    /// <summary>
    /// Gets option by ID with related members loaded
    /// </summary>
    /// <param name="id">Option ID</param>
    /// <returns>Bet option with members loaded, or null if not found</returns>
    Task<BetOption?> GetWithMembersAsync(int id);

    /// <summary>
    /// Deletes all options for a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    Task DeleteByBetIdAsync(int betId);
}
