using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using WebPush;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing Web Push notifications
/// Handles subscription management and sending push notifications using VAPID
/// Also delivers notifications to user inboxes as system messages
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly IPushSubscriptionRepository _subscriptionRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationUserSettingsRepository _settingsRepository;
    private readonly WebPushOptions _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _webPushClient;

    public PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IConversationUserSettingsRepository settingsRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _settingsRepository = settingsRepository;
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

    public async Task<bool> SubscribeAsync(string userId, PushSubscriptionDto subscription, string? userAgent = null, string? userName = null)
    {
        if (string.IsNullOrWhiteSpace(subscription.Endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(subscription));
        }

        var displayName = string.IsNullOrWhiteSpace(userName) ? "Unknown user" : userName;

        // Normalize keys: the WebPush library accepts both base64 and base64url,
        // but some Android browsers send base64url while others send standard base64.
        // Normalize to base64url (URL-safe, no padding) for consistent storage.
        var p256dh = NormalizeBase64Url(subscription.Keys.P256dh);
        var auth = NormalizeBase64Url(subscription.Keys.Auth);

        // Check if subscription already exists
        var existingSubscription = await _subscriptionRepository.GetByEndpointAsync(subscription.Endpoint);

        if (existingSubscription != null)
        {
            // Update existing subscription
            existingSubscription.UserId = userId;
            existingSubscription.P256dh = p256dh;
            existingSubscription.Auth = auth;
            existingSubscription.UserAgent = userAgent;
            existingSubscription.ExpirationTime = subscription.ExpirationTime;
            existingSubscription.UpdatedAt = DateTime.UtcNow;

            await _subscriptionRepository.UpdateAsync(existingSubscription);
            return false;
        }
        else
        {
            // Create new subscription
            var newSubscription = new Core.Entities.PushSubscription
            {
                UserId = userId,
                Endpoint = subscription.Endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
                ExpirationTime = subscription.ExpirationTime
            };

            await _subscriptionRepository.AddAsync(newSubscription);
            return true;
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

    public async Task<(int Sent, int Failed)> SendToSelectedUsersAsync(IEnumerable<string> userIds, SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            _logger.LogWarning("Cannot send push notification to selected users: WebPush is not configured");
            return (0, 0);
        }

        if (userIds == null || !userIds.Any())
        {
            _logger.LogWarning("Cannot send push notification: No user IDs provided");
            return (0, 0);
        }

        var userIdSet = userIds.ToHashSet();
        var allSubscriptions = await _subscriptionRepository.GetAllActiveAsync();
        var selectedSubscriptions = allSubscriptions.Where(s => userIdSet.Contains(s.UserId)).ToList();

        // Build a userId -> userName lookup from subscriptions' UserAgent field
        // Note: UserAgent stores browser info, not username. We'll use User navigation property if loaded,
        // otherwise track results by subscription for logging later.
        _logger.LogInformation("Sending push notification '{Title}' (tag: {Tag}) to {SubscriptionCount} subscriptions for {UserCount} users",
            notification.Title, notification.Tag ?? "none", selectedSubscriptions.Count, userIdSet.Count);

        var sent = 0;
        var failed = 0;

        // Send push notifications sequentially per subscription for better error tracking
        // (parallel sends with Task.WhenAll can swallow errors and cause rate limiting)
        foreach (var subscription in selectedSubscriptions)
        {
            var userName = subscription.User?.Nickname ?? subscription.User?.FirstName;
            var success = await SendNotificationWithResultAsync(subscription, notification, userName);
            if (success) sent++;
            else failed++;
        }

        // Also deliver to each recipient's inbox as a system message (fallback)
        foreach (var userId in userIdSet)
        {
            await SendInboxMessageAsync(userId, notification);
        }

        _logger.LogInformation("Push notification '{Title}' (tag: {Tag}) delivery complete: {Sent} sent, {Failed} failed out of {Total} subscriptions",
            notification.Title, notification.Tag ?? "none", sent, failed, selectedSubscriptions.Count);

        return (sent, failed);
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
    /// Sends a push notification to a specific subscription (fire-and-forget, no result)
    /// Handles failures and removes invalid subscriptions
    /// Implements retry logic for transient network errors
    /// </summary>
    private async Task SendNotificationAsync(Core.Entities.PushSubscription subscription, SendPushNotificationDto notification)
    {
        var userName = subscription.User?.Nickname ?? subscription.User?.FirstName;
        await SendNotificationWithResultAsync(subscription, notification, userName);
    }

    /// <summary>
    /// Sends a push notification to a specific subscription and returns success/failure
    /// Handles failures and removes invalid subscriptions
    /// Implements retry logic for transient network errors
    /// Sets TTL and Urgency headers for reliable delivery on iOS/Android
    /// </summary>
    private async Task<bool> SendNotificationWithResultAsync(Core.Entities.PushSubscription subscription, SendPushNotificationDto notification, string? userName = null)
    {
        // Use userName for logging, fallback to "unknown" if not available
        var displayName = !string.IsNullOrWhiteSpace(userName) ? userName : "unknown";

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
            tag = notification.Tag,
            unreadCount = notification.UnreadCount
        });

        // Set TTL (Time-To-Live) and Urgency headers
        // TTL: How long (seconds) the push service should hold the message if device is offline
        // Urgency: Helps push services on mobile decide whether to wake the device
        // "high" ensures iOS/Android deliver immediately instead of batching
        var options = new Dictionary<string, object>
        {
            { "TTL", 86400 },       // 24 hours - notification stays relevant for a day
            { "headers", new Dictionary<string, object>
                {
                    { "Urgency", "high" }   // high urgency = deliver immediately, wake device
                }
            }
        };

        // Total attempts = 1 initial + MaxRetryAttempts retries
        for (var attempt = 1; attempt <= MaxRetryAttempts + 1; attempt++)
        {
            try
            {
                await _webPushClient.SendNotificationAsync(pushSubscription, payload, options);
                return true; // Success
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                               ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription is no longer valid, remove it
                _logger.LogWarning("Push subscription {SubscriptionId} for user {UserName} is no longer valid ({StatusCode}), removing it",
                    subscription.Id, displayName, ex.StatusCode);
                await _subscriptionRepository.DeleteAsync(subscription);
                return false;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt <= MaxRetryAttempts)
            {
                // Rate limited - back off more aggressively
                var delay = TimeSpan.FromMilliseconds(InitialRetryDelay.TotalMilliseconds * Math.Pow(3, attempt));
                _logger.LogWarning("Push service rate limited for subscription {SubscriptionId}, retry {RetryAttempt} after {Delay}ms",
                    subscription.Id, attempt, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt <= MaxRetryAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(InitialRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                _logger.LogWarning(ex, "Transient error sending push notification to subscription {SubscriptionId}, retry {RetryAttempt} of {MaxRetries} after {Delay}ms",
                    subscription.Id, attempt, MaxRetryAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to subscription {SubscriptionId} for user {UserName}",
                    subscription.Id, displayName);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried
    /// </summary>
    private static bool IsTransientError(Exception? ex)
    {
        // Iterate through exception chain to check for transient errors
        while (ex != null)
        {
            // Check for HttpRequestException which wraps network errors
            if (ex is HttpRequestException)
                return true;

            // Check for IOException (e.g., Broken pipe)
            if (ex is IOException)
                return true;

            // Check for SocketException
            if (ex is SocketException)
                return true;

            // Move to inner exception
            ex = ex.InnerException;
        }

        return false;
    }

    /// <summary>
    /// Normalizes a base64 or base64url string to base64url format (URL-safe, no padding).
    /// Different browsers/devices may send keys in standard base64 (with +/=) or base64url (with -/_).
    /// The WebPush library handles both, but consistent storage prevents duplicate subscriptions.
    /// </summary>
    internal static string NormalizeBase64Url(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Convert standard base64 characters to base64url
        var result = input
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return result;
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

            // Auto-pin system conversation for the user (both new and existing)
            // This ensures existing conversations are also pinned for users who had them before this feature
            var settings = await _settingsRepository.GetOrCreateAsync(userId, conversation.Id);
            if (!settings.IsPinned)
            {
                settings.IsPinned = true;
                settings.UpdatedAt = DateTime.UtcNow;
                await _settingsRepository.UpdateAsync(settings);
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

            // Update conversation directly — it's already tracked from the query/add above.
            // No need to reload; GetSystemConversationForUserAsync returns a tracked entity,
            // and newly-created conversations are tracked after AddAsync.
            conversation.LastMessageAt = message.CreatedAt;
            conversation.LastMessageId = message.Id;
            await _conversationRepository.UpdateAsync(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending inbox message");
        }
    }
}
