using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

// Web Push: send to one user, everyone or selected users.
public partial class PushNotificationService
{
    public async Task SendToUserAsync(string userId, SendPushNotificationDto notification)
    {
        await KeepAsync(new[] { userId }, notification);
        if (!_options.IsConfigured())
        {
            return;
        }

        var muted = await MutedAsync(notification);
        if (muted.Contains(userId))
        {
            _logger.LogDebug("Push '{Tag}' not sent: the member turned {Key} off", notification.Tag ?? "none", notification.PreferenceKey);
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
        await KeepAsync(null, notification);
        if (!_options.IsConfigured())
        {
            return;
        }

        var muted = await MutedAsync(notification);
        var subscriptions = (await _subscriptionRepository.GetAllActiveAsync()).Where(s => !muted.Contains(s.UserId));

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
        if (userIds?.Any() == true)
        {
            await KeepAsync(userIds, notification);
        }

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

        var muted = await MutedAsync(notification);
        var userIdSet = userIds.ToHashSet();
        var allSubscriptions = await _subscriptionRepository.GetAllActiveAsync();
        var selectedSubscriptions = allSubscriptions.Where(s => userIdSet.Contains(s.UserId) && !muted.Contains(s.UserId)).ToList();

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

    /// <summary>
    /// Every push is also kept in the recipients' "Notificações RTUB" (task 058), before the push itself and whether or
    /// not push is configured: a member without push still finds it in /messages.
    /// </summary>
    private Task KeepAsync(IEnumerable<string>? userIds, SendPushNotificationDto notification) =>
        _inbox?.KeepAsync(userIds, notification) ?? Task.CompletedTask;

    /// <summary>
    /// Task 082: the accounts that turned this push's key off on /profile, applied here so every send path (one member,
    /// selected members, broadcast, background jobs, the Owner's text) honours it and a stale client cannot bypass it.
    /// Decided after the inbox copy, which a preference never stops. A push without a key (the test push) and the
    /// mandatory key mute nobody.
    /// </summary>
    private async Task<IReadOnlySet<string>> MutedAsync(SendPushNotificationDto notification) =>
        _preferences is null || string.IsNullOrEmpty(notification.PreferenceKey)
            ? new HashSet<string>()
            : await _preferences.MutedUserIdsAsync(notification.PreferenceKey);
}
