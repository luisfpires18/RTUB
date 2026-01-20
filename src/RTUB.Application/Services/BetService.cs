using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Bet service implementation using Repository pattern
/// Contains business logic for betting operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class BetService : IBetService
{
    private readonly IBetRepository _betRepository;
    private readonly IBetOptionRepository _betOptionRepository;
    private readonly IUserBetRepository _userBetRepository;
    private readonly IBetCommentRepository _betCommentRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public BetService(
        IBetRepository betRepository,
        IBetOptionRepository betOptionRepository,
        IUserBetRepository userBetRepository,
        IBetCommentRepository betCommentRepository,
        UserManager<ApplicationUser> userManager)
    {
        _betRepository = betRepository;
        _betOptionRepository = betOptionRepository;
        _userBetRepository = userBetRepository;
        _betCommentRepository = betCommentRepository;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets all future bets (not yet occurred and not cancelled)
    /// </summary>
    /// <returns>Collection of future bets ordered by datetime</returns>
    public async Task<IEnumerable<Bet>> GetFutureBetsAsync()
    {
        return await _betRepository.GetFutureBetsAsync();
    }

    /// <summary>
    /// Gets all past bets (already occurred)
    /// </summary>
    /// <returns>Collection of past bets ordered by datetime descending</returns>
    public async Task<IEnumerable<Bet>> GetPastBetsAsync()
    {
        return await _betRepository.GetPastBetsAsync();
    }

    /// <summary>
    /// Gets a bet by its ID with all related data
    /// </summary>
    /// <param name="id">Bet ID</param>
    /// <returns>Bet with details, or null if not found</returns>
    public async Task<Bet?> GetBetByIdAsync(int id)
    {
        return await _betRepository.GetBetWithDetailsAsync(id);
    }

    /// <summary>
    /// Creates a new bet
    /// </summary>
    /// <param name="bet">Bet entity to create</param>
    /// <returns>Created bet</returns>
    public async Task<Bet> CreateBetAsync(Bet bet)
    {
        if (bet == null)
            throw new ArgumentNullException(nameof(bet));

        return await _betRepository.AddAsync(bet);
    }

    /// <summary>
    /// Updates an existing bet
    /// </summary>
    /// <param name="bet">Bet entity with updated values</param>
    public async Task UpdateBetAsync(Bet bet)
    {
        if (bet == null)
            throw new ArgumentNullException(nameof(bet));

        var existingBet = await _betRepository.GetByIdAsync(bet.Id);
        if (existingBet == null)
            throw new EntityNotFoundException(nameof(Bet), bet.Id);

        await _betRepository.UpdateAsync(bet);
    }

    /// <summary>
    /// Deletes a bet and all associated user bets
    /// </summary>
    /// <param name="id">Bet ID to delete</param>
    public async Task DeleteBetAsync(int id)
    {
        var bet = await _betRepository.GetByIdAsync(id);
        if (bet == null)
            throw new EntityNotFoundException(nameof(Bet), id);

        // Delete related entities first due to FK constraints
        // Using ExecuteDeleteAsync for efficient bulk deletion
        await _userBetRepository.DeleteByBetIdAsync(id);
        await _betOptionRepository.DeleteByBetIdAsync(id);
        await _betCommentRepository.DeleteByBetIdAsync(id);

        // Finally delete the bet
        await _betRepository.DeleteByIdDirectAsync(id);
    }

    /// <summary>
    /// Resolves a bet by setting the winning option and calculating winnings
    /// Updates user balances and marks all user bets as won/lost
    /// </summary>
    /// <param name="betId">Bet ID to resolve</param>
    /// <param name="winningOptionId">Winning option ID</param>
    public async Task ResolveBetAsync(int betId, int winningOptionId)
    {
        // Load bet with all details
        var bet = await _betRepository.GetBetWithDetailsAsync(betId);
        if (bet == null)
            throw new EntityNotFoundException(nameof(Bet), betId);

        // Verify the winning option exists and belongs to this bet
        var winningOption = bet.Options.FirstOrDefault(o => o.Id == winningOptionId);
        if (winningOption == null)
            throw new ArgumentException("A opção vencedora não pertence a esta aposta", nameof(winningOptionId));

        // Mark bet as resolved
        bet.Resolve(winningOptionId);
        await _betRepository.UpdateAsync(bet);

        // Process all user bets
        var userBets = await _userBetRepository.GetByBetIdAsync(betId);

        // Collect all unique user IDs for batch loading
        var userIds = userBets.Select(ub => ub.UserId).Distinct().ToList();

        // Batch load all users at once to avoid N+1 queries
        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        var userDict = users.ToDictionary(u => u.Id);

        // Process all user bets and update balances
        foreach (var userBet in userBets)
        {
            if (userBet.BetOptionId == winningOptionId)
            {
                // User won - calculate winnings
                userBet.MarkAsWon(winningOption.Odds);

                // Update user balance using batch-loaded user
                if (userDict.TryGetValue(userBet.UserId, out var user))
                {
                    user.FidelisBalance += userBet.FidelisWinnings;
                }
            }
            else
            {
                // User lost
                userBet.MarkAsLost();
            }

            // Update user bet in repository
            await _userBetRepository.UpdateAsync(userBet);
        }

        // Batch update all users at once
        foreach (var user in userDict.Values)
        {
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Places a bet for a user on a specific option
    /// Validates user has sufficient balance and deducts the amount
    /// </summary>
    /// <param name="userId">User ID placing the bet</param>
    /// <param name="betId">Bet ID</param>
    /// <param name="optionId">Option ID to bet on</param>
    /// <param name="fidelisAmount">Amount of Fidelis to wager</param>
    /// <returns>Created user bet</returns>
    public async Task<UserBet> PlaceBetAsync(string userId, int betId, int optionId, decimal fidelisAmount)
    {
        // Validate user exists
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        // Validate bet exists and is still open
        var bet = await _betRepository.GetBetWithOptionsAsync(betId);
        if (bet == null)
            throw new EntityNotFoundException(nameof(Bet), betId);

        if (bet.IsCancelled)
            throw new InvalidOperationException("Esta aposta foi cancelada");

        if (bet.IsResolved())
            throw new InvalidOperationException("Esta aposta já foi resolvida");

        if (bet.IsPast())
            throw new InvalidOperationException("Não é possível apostar numa aposta que já ocorreu");

        // Validate option exists and belongs to this bet
        var option = bet.Options.FirstOrDefault(o => o.Id == optionId);
        if (option == null)
            throw new ArgumentException("A opção não pertence a esta aposta", nameof(optionId));

        // Check if user already placed a bet on this bet
        var existingUserBet = await _userBetRepository.GetUserBetForBetAsync(userId, betId);
        if (existingUserBet != null)
            throw new InvalidOperationException("Já apostou nesta aposta");

        // Validate user has sufficient balance
        if (user.FidelisBalance < fidelisAmount)
            throw new InvalidOperationException("Saldo de Fidelis insuficiente");

        // Deduct amount from user balance
        user.FidelisBalance -= fidelisAmount;
        await _userManager.UpdateAsync(user);

        // Create user bet
        var userBet = UserBet.Create(userId, betId, optionId, fidelisAmount);
        return await _userBetRepository.AddAsync(userBet);
    }

    /// <summary>
    /// Gets all bets placed by a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Collection of user bets ordered by creation date</returns>
    public async Task<IEnumerable<UserBet>> GetUserBetsAsync(string userId)
    {
        return await _userBetRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Gets all options for a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    /// <returns>Collection of bet options</returns>
    public async Task<IEnumerable<BetOption>> GetBetOptionsAsync(int betId)
    {
        return await _betOptionRepository.GetByBetIdAsync(betId);
    }

    /// <summary>
    /// Cancels a bet and refunds all user bets
    /// </summary>
    /// <param name="betId">Bet ID to cancel</param>
    /// <param name="reason">Cancellation reason</param>
    public async Task CancelBetAsync(int betId, string reason)
    {
        var bet = await _betRepository.GetByIdAsync(betId);
        if (bet == null)
            throw new EntityNotFoundException(nameof(Bet), betId);

        // Cancel the bet
        bet.Cancel(reason);
        await _betRepository.UpdateAsync(bet);

        // Refund all user bets
        var userBets = await _userBetRepository.GetByBetIdAsync(betId);

        // Collect all unique user IDs for batch loading
        var userIds = userBets.Select(ub => ub.UserId).Distinct().ToList();

        // Batch load all users at once to avoid N+1 queries
        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        var userDict = users.ToDictionary(u => u.Id);

        // Process refunds
        foreach (var userBet in userBets)
        {
            // Refund the amount to user using batch-loaded user
            if (userDict.TryGetValue(userBet.UserId, out var user))
            {
                user.FidelisBalance += userBet.FidelisAmount;
            }
        }

        // Batch update all users at once
        foreach (var user in userDict.Values)
        {
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Uncancels a bet
    /// Note: Does not recreate user bets - users were already refunded
    /// </summary>
    /// <param name="betId">Bet ID to uncancel</param>
    public async Task UncancelBetAsync(int betId)
    {
        var bet = await _betRepository.GetByIdAsync(betId);
        if (bet == null)
            throw new EntityNotFoundException(nameof(Bet), betId);

        bet.Uncancel();
        await _betRepository.UpdateAsync(bet);
    }
}
