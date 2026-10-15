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
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly IPushSubscriptionRepository _subscriptionRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly WebPushOptions _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _webPushClient;

    public PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IUserProfileRepository userProfileRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger)
        : this(subscriptionRepository, userProfileRepository, options, logger, null)
    {
    }

    /// <summary>Tests pass an <see cref="HttpClient"/> over a fake handler, so no real push service is called.</summary>
    internal PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IUserProfileRepository userProfileRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger,
        HttpClient? httpClient)
    {
        _subscriptionRepository = subscriptionRepository;
        _userProfileRepository = userProfileRepository;
        _options = options.Value;
        _logger = logger;
        _webPushClient = httpClient is null ? new WebPushClient() : new WebPushClient(httpClient);

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

    public async Task<int> RemoveForUserAsync(string userId, string? endpoint)
    {
        var mine = (await _subscriptionRepository.GetByUserIdAsync(userId)).ToList();
        var removed = endpoint is null ? mine : mine.Where(s => s.Endpoint == endpoint).ToList();

        foreach (var subscription in removed)
        {
            await _subscriptionRepository.DeleteByEndpointAsync(subscription.Endpoint);
        }

        return mine.Count - removed.Count;
    }

    public async Task<int> CountForUserAsync(string userId) =>
        (await _subscriptionRepository.GetByUserIdAsync(userId)).Count();

    public async Task<bool> IsRegisteredAsync(string userId, string endpoint) =>
        await _subscriptionRepository.GetByEndpointAsync(endpoint) is { } subscription && subscription.UserId == userId;

    public async Task SendToUserAsync(string userId, SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            return;
        }

        var results = new List<PushSendResult>();
        foreach (var subscription in await _subscriptionRepository.GetByUserIdAsync(userId))
        {
            results.Add(await SendNotificationWithResultAsync(subscription, notification));
        }

        LogSummary(notification, results);
    }

    public async Task BroadcastAsync(SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            return;
        }

        var subscriptions = await _subscriptionRepository.GetAllActiveAsync();

        var results = await Task.WhenAll(subscriptions.Select(subscription => SendNotificationWithResultAsync(subscription, notification)));
        LogSummary(notification, results);
    }

    public async Task<PushSendResult> SendTestAsync(string userId, string endpoint, SendPushNotificationDto notification)
    {
        if (!_options.IsConfigured())
        {
            return new PushSendResult(PushDeliveryOutcome.NotConfigured);
        }

        // Only the caller's own subscription, found by this browser's endpoint: never another member's device.
        var subscription = (await _subscriptionRepository.GetByUserIdAsync(userId)).FirstOrDefault(s => s.Endpoint == endpoint);
        return subscription is null
            ? new PushSendResult(PushDeliveryOutcome.NotRegistered)
            : await SendNotificationWithResultAsync(subscription, notification);
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
            var result = await SendNotificationWithResultAsync(subscription, notification);
            if (result.Outcome == PushDeliveryOutcome.Sent) sent++;
            else failed++;
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

    public async Task<bool> IsOptedOutAsync(string userId)
    {
        var user = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == userId);
        return user?.PushNotificationsOptedOut ?? false;
    }

    public async Task SetOptedOutAsync(string userId, bool optedOut)
    {
        var user = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.PushNotificationsOptedOut == optedOut)
        {
            return;
        }

        user.PushNotificationsOptedOut = optedOut;
        await _userProfileRepository.UpdateAsync(user);
    }

    /// <summary>
    /// One line per fan-out (#230): a warning when anything failed, so a delivery problem shows in the logs without a
    /// line per successful send.
    /// </summary>
    private void LogSummary(SendPushNotificationDto notification, IReadOnlyCollection<PushSendResult> results)
    {
        var sent = results.Count(r => r.Outcome == PushDeliveryOutcome.Sent);
        var gone = results.Count(r => r.Outcome == PushDeliveryOutcome.Gone);
        var failed = results.Count - sent - gone;
        _logger.Log(failed > 0 ? LogLevel.Warning : LogLevel.Debug,
            "Push '{Tag}': {Sent} sent, {Gone} stale removed, {Failed} failed of {Total} subscriptions",
            notification.Tag ?? "none", sent, gone, failed, results.Count);
    }

    /// <summary>
    /// Sends a push notification to one subscription and says what the push service answered (#230). Success is a 2xx
    /// from the push service, nothing more. 404 / 410 remove the subscription; every other failure keeps it. Retries
    /// transient network errors and 429. Sets TTL and Urgency headers for reliable delivery on iOS / Android. Logs the
    /// subscription id and push service host, never the endpoint path or the keys.
    /// </summary>
    private async Task<PushSendResult> SendNotificationWithResultAsync(Core.Entities.PushSubscription subscription, SendPushNotificationDto notification)
    {
        var displayName = subscription.User?.Nickname ?? subscription.User?.FirstName ?? "unknown";
        var host = Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : "invalid";
        var tag = notification.Tag ?? "none";

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
                return new PushSendResult(PushDeliveryOutcome.Sent);
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Push '{Tag}' to subscription {SubscriptionId} ({Host}) of {UserName}: {StatusCode}, subscription gone, removing it",
                    tag, subscription.Id, host, displayName, (int)ex.StatusCode);
                try
                {
                    await _subscriptionRepository.DeleteAsync(subscription);
                }
                catch (Exception deleteEx)
                {
                    // A failed cleanup must not stop the fan-out to the member's other devices; the next send retries it.
                    _logger.LogWarning(deleteEx, "Could not remove gone push subscription {SubscriptionId}", subscription.Id);
                }
                return new PushSendResult(PushDeliveryOutcome.Gone, (int)ex.StatusCode);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt <= MaxRetryAttempts)
            {
                // Rate limited - back off more aggressively
                var delay = TimeSpan.FromMilliseconds(InitialRetryDelay.TotalMilliseconds * Math.Pow(3, attempt));
                _logger.LogWarning("Push service rate limited for subscription {SubscriptionId}, retry {RetryAttempt} after {Delay}ms",
                    subscription.Id, attempt, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
            catch (WebPushException ex)
            {
                // 400 / 401 / 403 / 413 / 5xx / 429 after the retries: the subscription stays (a VAPID or server problem
                // is not the device's fault), and the push service's own reason goes in the log.
                var status = (int)ex.StatusCode;
                var outcome = status == 429 ? PushDeliveryOutcome.RateLimited
                    : status >= 500 ? PushDeliveryOutcome.ProviderError
                    : PushDeliveryOutcome.Rejected;
                _logger.LogWarning("Push '{Tag}' to subscription {SubscriptionId} ({Host}) of {UserName} refused: {StatusCode} {Outcome} {Reason}",
                    tag, subscription.Id, host, displayName, status, outcome, await ReasonAsync(ex));
                return new PushSendResult(outcome, status);
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
                var outcome = IsTransientError(ex) ? PushDeliveryOutcome.NetworkError : PushDeliveryOutcome.Failed;
                _logger.LogError(ex, "Push '{Tag}' to subscription {SubscriptionId} ({Host}) of {UserName} failed: {Outcome}",
                    tag, subscription.Id, host, displayName, outcome);
                return new PushSendResult(outcome);
            }
        }

        return new PushSendResult(PushDeliveryOutcome.Failed);
    }

    /// <summary>
    /// The push service's short reason (Apple: <c>{"reason":"BadJwtToken"}</c>, FCM / Mozilla: a sentence), capped.
    /// It describes the request, never the subscription keys.
    /// </summary>
    private static async Task<string> ReasonAsync(WebPushException ex)
    {
        try
        {
            var body = ex.HttpResponseMessage is null ? "" : await ex.HttpResponseMessage.Content.ReadAsStringAsync();
            body = body.ReplaceLineEndings(" ").Trim();
            return body.Length > 200 ? body[..200] : body;
        }
        catch (Exception)
        {
            return "";
        }
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
}
