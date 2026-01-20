using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for UserBet entity with domain-specific operations
/// Extends generic repository with user bet-specific queries
/// </summary>
public interface IUserBetRepository : IRepository<UserBet>
{
    /// <summary>
    /// Gets all bets placed by a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Collection of user bets ordered by creation date descending</returns>
    Task<IEnumerable<UserBet>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets all bets placed on a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    /// <returns>Collection of user bets for the bet</returns>
    Task<IEnumerable<UserBet>> GetByBetIdAsync(int betId);

    /// <summary>
    /// Gets user's bet for a specific bet (if exists)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="betId">Bet ID</param>
    /// <returns>User bet if exists, otherwise null</returns>
    Task<UserBet?> GetUserBetForBetAsync(string userId, int betId);

    /// <summary>
    /// Gets all user bets for a specific bet option
    /// </summary>
    /// <param name="betOptionId">Bet option ID</param>
    /// <returns>Collection of user bets for the option</returns>
    Task<IEnumerable<UserBet>> GetByBetOptionIdAsync(int betOptionId);

    /// <summary>
    /// Deletes all user bets for a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    Task DeleteByBetIdAsync(int betId);
}
