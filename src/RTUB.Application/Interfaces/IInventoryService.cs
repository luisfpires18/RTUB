namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for inventory operations
/// Provides high-level business logic for inventory management
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Uses a beer to heal the user's character
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing success status, healed amount, and message</returns>
    Task<(bool Success, int HealedAmount, string Message)> UseBeerAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quantity of beer in the user's inventory
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The quantity of beer, or 0 if none</returns>
    Task<int> GetBeerQuantityAsync(string userId, CancellationToken cancellationToken = default);
}
