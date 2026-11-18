using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using WebPush;

namespace RTUB.Application.Services;

// Web Push: one delivery with retries, failure reasons and key normalization.
public partial class PushNotificationService
{
    /// <summary>
    /// One line per fan-out: a warning when anything failed, so a delivery problem shows in the logs without a
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
    /// Sends a push notification to one subscription and says what the push service answered. Success is a 2xx
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
