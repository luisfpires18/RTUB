using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using WebPush;

namespace RTUB.Application.Services;

// Web Push: subscriptions, VAPID key and the opt-out flag.
public partial class PushNotificationService
{
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
}
