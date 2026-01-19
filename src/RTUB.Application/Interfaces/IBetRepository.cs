using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Bet entity with domain-specific operations
/// Extends generic repository with bet-specific queries
/// </summary>
public interface IBetRepository : IRepository<Bet>
{
    /// <summary>
    /// Gets future bets (bets with datetime > now and not cancelled)
    /// </summary>
    /// <returns>Collection of future bets ordered by datetime</returns>
    Task<IEnumerable<Bet>> GetFutureBetsAsync();

    /// <summary>
    /// Gets past bets (bets with datetime <= now)
    /// </summary>
    /// <returns>Collection of past bets ordered by datetime descending</returns>
    Task<IEnumerable<Bet>> GetPastBetsAsync();

    /// <summary>
    /// Gets bet by ID with all related options loaded
    /// </summary>
    /// <param name="id">Bet ID</param>
    /// <returns>Bet with options loaded, or null if not found</returns>
    Task<Bet?> GetBetWithOptionsAsync(int id);

    /// <summary>
    /// Gets bet by ID with options and user bets loaded
    /// </summary>
    /// <param name="id">Bet ID</param>
    /// <returns>Bet with options and user bets loaded, or null if not found</returns>
    Task<Bet?> GetBetWithDetailsAsync(int id);
}
