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
/// Also delivers notifications to user inboxes as system messages
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private readonly IPushSubscriptionRepository _subscriptionRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly WebPushOptions _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _webPushClient;

    public PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
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

        var displayName = string.IsNullOrWhiteSpace(userName) ? "Unknown user" : userName;

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
        }
    }

    public async Task UnsubscribeAsync(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));
        }

        await _subscriptionRepository.DeleteByEndpointAsync(endpoint);
    }

    public async Task SendToUserAsync(string userId, SendPushNotificationDto notification)
    {
        // Always deliver to user's inbox as a system message, even if push is not configured
        await SendInboxMessageAsync(userId, notification);
        
        if (!_options.IsConfigured())
        {
            return;
        }

        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
        
        foreach (var subscription in subscriptions)
        {
            await SendNotificationAsync(subscription, notification);
        }
    }

    public async Task SendPushOnlyAsync(string userId, SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            return;
        }

        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
        
        foreach (var subscription in subscriptions)
        {
            await SendNotificationAsync(subscription, notification);
        }
        
        // Note: No inbox message is created - this is intentional for direct message notifications
        // since the actual message is already in the conversation
    }

    public async Task BroadcastAsync(SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            return;
        }

        var subscriptions = await _subscriptionRepository.GetAllActiveAsync();
        
        var tasks = subscriptions.Select(subscription => SendNotificationAsync(subscription, notification));
        await Task.WhenAll(tasks);

        // Also deliver to each recipient's inbox as a system message
        var userIds = subscriptions.Select(s => s.UserId).Distinct();
        foreach (var userId in userIds)
        {
            await SendInboxMessageAsync(userId, notification);
        }
    }

    public async Task SendToSelectedUsersAsync(IEnumerable<string> userIds, SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            _logger.LogWarning("Cannot send push notification to selected users: WebPush is not configured");
            return;
        }

        if (userIds == null || !userIds.Any())
        {
            _logger.LogWarning("Cannot send push notification: No user IDs provided");
            return;
        }

        var allSubscriptions = await _subscriptionRepository.GetAllActiveAsync();
        var selectedSubscriptions = allSubscriptions.Where(s => userIds.Contains(s.UserId)).ToList();
        
        var tasks = selectedSubscriptions.Select(subscription => SendNotificationAsync(subscription, notification));
        await Task.WhenAll(tasks);
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

    /// <summary>
    /// Sends a system message to a user's inbox
    /// Creates or gets the system conversation and adds the message
    /// </summary>
    private async Task SendInboxMessageAsync(string userId, SendPushNotificationDto notification)
    {
        try
        {
            // Build message body from notification title and body
            var messageBody = string.IsNullOrWhiteSpace(notification.Title)
                ? notification.Body
                : $"{notification.Title}\n\n{notification.Body}";

            // Get or create system conversation for this user
            var conversation = await _conversationRepository.GetSystemConversationForUserAsync(userId);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    Participants = userId,
                    LastMessageAt = DateTime.UtcNow,
                    IsSystemConversation = true,
                    Title = "Sistema RTUB",
                    CreatedAt = DateTime.UtcNow
                };
                await _conversationRepository.AddAsync(conversation);
            }

            // Create system message
            var message = new Message
            {
                ConversationId = conversation.Id,
                SenderId = null,
                Body = messageBody,
                IsSystem = true,
                Link = notification.Url,
                CreatedAt = DateTime.UtcNow
            };

            await _messageRepository.AddAsync(message);

            // Update conversation
            conversation.LastMessageAt = message.CreatedAt;
            conversation.LastMessageId = message.Id;
            await _conversationRepository.UpdateAsync(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending inbox message to user {UserId}", userId);
        }
    }
}
