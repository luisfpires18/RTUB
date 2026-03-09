using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Bet operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IBetService
{
    /// <summary>
    /// Gets all future bets (not yet occurred and not cancelled)
    /// </summary>
    /// <returns>Collection of future bets</returns>
    Task<IEnumerable<Bet>> GetFutureBetsAsync();

    /// <summary>
    /// Gets all past bets (already occurred)
    /// </summary>
    /// <returns>Collection of past bets</returns>
    Task<IEnumerable<Bet>> GetPastBetsAsync();

    /// <summary>
    /// Gets a bet by its ID with all related data
    /// </summary>
    /// <param name="id">Bet ID</param>
    /// <returns>Bet with details, or null if not found</returns>
    Task<Bet?> GetBetByIdAsync(int id);

    /// <summary>
    /// Creates a new bet
    /// </summary>
    /// <param name="bet">Bet entity to create</param>
    /// <returns>Created bet</returns>
    Task<Bet> CreateBetAsync(Bet bet);

    /// <summary>
    /// Updates an existing bet
    /// </summary>
    /// <param name="bet">Bet entity with updated values</param>
    Task UpdateBetAsync(Bet bet);

    /// <summary>
    /// Deletes a bet
    /// </summary>
    /// <param name="id">Bet ID to delete</param>
    Task DeleteBetAsync(int id);

    /// <summary>
    /// Resolves a bet by setting the winning option and calculating winnings
    /// </summary>
    /// <param name="betId">Bet ID to resolve</param>
    /// <param name="winningOptionId">Winning option ID</param>
    Task ResolveBetAsync(int betId, int winningOptionId);

    /// <summary>
    /// Places a bet for a user on a specific option
    /// </summary>
    /// <param name="userId">User ID placing the bet</param>
    /// <param name="betId">Bet ID</param>
    /// <param name="optionId">Option ID to bet on</param>
    /// <param name="fidelisAmount">Amount of Fidelis to wager</param>
    /// <returns>Created user bet</returns>
    Task<UserBet> PlaceBetAsync(string userId, int betId, int optionId, decimal fidelisAmount);

    /// <summary>
    /// Gets all bets placed by a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Collection of user bets</returns>
    Task<IEnumerable<UserBet>> GetUserBetsAsync(string userId);

    /// <summary>
    /// Gets all options for a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    /// <returns>Collection of bet options</returns>
    Task<IEnumerable<BetOption>> GetBetOptionsAsync(int betId);

    /// <summary>
    /// Gets all options for multiple bets (batch operation to avoid N+1 queries)
    /// </summary>
    /// <param name="betIds">Collection of bet IDs</param>
    /// <returns>Dictionary mapping bet ID to list of options</returns>
    Task<Dictionary<int, List<BetOption>>> GetBetOptionsByBetIdsAsync(IEnumerable<int> betIds);

    /// <summary>
    /// Cancels a bet and refunds all user bets
    /// </summary>
    /// <param name="betId">Bet ID to cancel</param>
    /// <param name="reason">Cancellation reason</param>
    Task CancelBetAsync(int betId, string reason);

    /// <summary>
    /// Uncancels a bet
    /// </summary>
    /// <param name="betId">Bet ID to uncancel</param>
    Task UncancelBetAsync(int betId);

    /// <summary>
    /// Approves a bet for resolution by the creator. Only admins can approve.
    /// </summary>
    /// <param name="betId">Bet ID to approve</param>
    Task ApproveBetAsync(int betId);
}
