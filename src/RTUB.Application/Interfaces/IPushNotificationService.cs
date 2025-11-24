using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for managing push notifications
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Subscribes a user to push notifications
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="subscription">The push subscription details</param>
    /// <param name="userAgent">Optional user agent information</param>
    Task SubscribeAsync(string userId, PushSubscriptionDto subscription, string? userAgent = null);

    /// <summary>
    /// Unsubscribes from push notifications by endpoint
    /// </summary>
    /// <param name="endpoint">The subscription endpoint</param>
    Task UnsubscribeAsync(string endpoint);

    /// <summary>
    /// Sends a push notification to a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="notification">The notification to send</param>
    Task SendToUserAsync(string userId, SendPushNotificationDto notification);

    /// <summary>
    /// Sends a push notification to all subscribed users
    /// </summary>
    /// <param name="notification">The notification to send</param>
    Task BroadcastAsync(SendPushNotificationDto notification);

    /// <summary>
    /// Gets the VAPID public key for client-side subscription
    /// </summary>
    /// <returns>The VAPID public key</returns>
    string GetVapidPublicKey();

    /// <summary>
    /// Checks if Web Push is properly configured
    /// </summary>
    /// <returns>True if configured, otherwise false</returns>
    bool IsConfigured();

    /// <summary>
    /// Gets all distinct user IDs that have active push subscriptions
    /// </summary>
    /// <returns>List of user IDs with active push subscriptions</returns>
    Task<IEnumerable<string>> GetSubscribedUserIdsAsync();
}
