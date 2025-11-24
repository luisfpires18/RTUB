using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WebPush;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing Web Push notifications
/// Handles subscription management and sending push notifications using VAPID
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private readonly IPushSubscriptionRepository _subscriptionRepository;
    private readonly WebPushOptions _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _webPushClient;

    public PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _options = options.Value;
        _logger = logger;
        _webPushClient = new WebPushClient();

        // Configure VAPID details if available and valid
        if (_options.IsConfigured())
        {
            try
            {
                _webPushClient.SetVapidDetails(
                    _options.VapidSubject,
                    _options.VapidPublicKey,
                    _options.VapidPrivateKey);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid VAPID configuration. Web Push will not be functional.");
            }
        }
    }

    public async Task SubscribeAsync(string userId, PushSubscriptionDto subscription, string? userAgent = null, string? userName = null)
    {
        if (string.IsNullOrWhiteSpace(subscription.Endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(subscription));
        }

        var displayName = string.IsNullOrWhiteSpace(userName) ? userId : userName;

        // Check if subscription already exists
        var existingSubscription = await _subscriptionRepository.GetByEndpointAsync(subscription.Endpoint);
        
        if (existingSubscription != null)
        {
            // Update existing subscription
            existingSubscription.UserId = userId;
            existingSubscription.P256dh = subscription.Keys.P256dh;
            existingSubscription.Auth = subscription.Keys.Auth;
            existingSubscription.UserAgent = userAgent;
            existingSubscription.ExpirationTime = subscription.ExpirationTime;
            existingSubscription.UpdatedAt = DateTime.UtcNow;
            
            await _subscriptionRepository.UpdateAsync(existingSubscription);
            _logger.LogInformation("Updated push subscription for user {UserName}", displayName);
        }
        else
        {
            // Create new subscription
            var newSubscription = new Core.Entities.PushSubscription
            {
                UserId = userId,
                Endpoint = subscription.Endpoint,
                P256dh = subscription.Keys.P256dh,
                Auth = subscription.Keys.Auth,
                UserAgent = userAgent,
                ExpirationTime = subscription.ExpirationTime
            };

            await _subscriptionRepository.AddAsync(newSubscription);
            _logger.LogInformation("Created new push subscription for user {UserName}", displayName);
        }
    }

    public async Task UnsubscribeAsync(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));
        }

        await _subscriptionRepository.DeleteByEndpointAsync(endpoint);
        _logger.LogInformation("Deleted push subscription with endpoint {Endpoint}", endpoint);
    }

    public async Task SendToUserAsync(string userId, SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            _logger.LogWarning("Cannot send push notification: WebPush is not configured");
            return;
        }

        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
        
        foreach (var subscription in subscriptions)
        {
            await SendNotificationAsync(subscription, notification);
        }
    }

    public async Task BroadcastAsync(SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            _logger.LogWarning("Cannot broadcast push notification: WebPush is not configured");
            return;
        }

        var subscriptions = await _subscriptionRepository.GetAllActiveAsync();
        
        var tasks = subscriptions.Select(subscription => SendNotificationAsync(subscription, notification));
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Broadcast push notification to {Count} subscriptions", subscriptions.Count());
    }

    public string GetVapidPublicKey()
    {
        return _options.VapidPublicKey;
    }

    public bool IsConfigured()
    {
        return _options.IsConfigured();
    }

    public async Task<IEnumerable<string>> GetSubscribedUserIdsAsync()
    {
        var subscriptions = await _subscriptionRepository.GetAllActiveAsync();
        return subscriptions.Select(s => s.UserId).Distinct();
    }

    /// <summary>
    /// Sends a push notification to a specific subscription
    /// Handles failures and removes invalid subscriptions
    /// </summary>
    private async Task SendNotificationAsync(Core.Entities.PushSubscription subscription, SendPushNotificationDto notification)
    {
        try
        {
            var pushSubscription = new WebPush.PushSubscription(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth);

            var payload = JsonSerializer.Serialize(new
            {
                title = notification.Title,
                body = notification.Body,
                icon = notification.Icon ?? "/icons/rtub-logo-192.png",
                url = notification.Url ?? "/",
                tag = notification.Tag
            });

            await _webPushClient.SendNotificationAsync(pushSubscription, payload);
            
            _logger.LogDebug("Sent push notification to subscription {SubscriptionId}", subscription.Id);
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || 
                                           ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Subscription is no longer valid, remove it
            _logger.LogWarning("Push subscription {SubscriptionId} is no longer valid, removing it", subscription.Id);
            await _subscriptionRepository.DeleteAsync(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification to subscription {SubscriptionId}", subscription.Id);
        }
    }
}
