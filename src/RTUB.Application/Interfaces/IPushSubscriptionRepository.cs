using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for managing push subscriptions
/// </summary>
public interface IPushSubscriptionRepository : IRepository<PushSubscription>
{
    /// <summary>
    /// Gets all active subscriptions for a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>List of active subscriptions</returns>
    Task<IEnumerable<PushSubscription>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets a subscription by endpoint URL
    /// </summary>
    /// <param name="endpoint">The subscription endpoint</param>
    /// <returns>The subscription if found, otherwise null</returns>
    Task<PushSubscription?> GetByEndpointAsync(string endpoint);

    /// <summary>
    /// Deletes a subscription by endpoint
    /// </summary>
    /// <param name="endpoint">The subscription endpoint</param>
    Task DeleteByEndpointAsync(string endpoint);

    /// <summary>
    /// Gets all active subscriptions (for broadcast notifications)
    /// </summary>
    /// <returns>List of all active subscriptions</returns>
    Task<IEnumerable<PushSubscription>> GetAllActiveAsync();
}
