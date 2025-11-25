using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for messaging integration
/// </summary>
public static class MessagingExtensions
{
    /// <summary>
    /// Sends both a push notification and a system message to a user
    /// Use this when you want to notify users about events, rehearsals, meetings, etc.
    /// </summary>
    /// <param name="pushService">Push notification service</param>
    /// <param name="messagingService">Messaging service</param>
    /// <param name="userId">Target user ID</param>
    /// <param name="notification">Push notification DTO</param>
    /// <param name="messageBody">Optional custom message body (uses notification body if null)</param>
    /// <returns></returns>
    public static async Task SendNotificationWithMessageAsync(
        this IPushNotificationService pushService,
        IMessagingService messagingService,
        string userId,
        SendPushNotificationDto notification,
        string? messageBody = null)
    {
        try
        {
            // Send push notification
            await pushService.SendToUserAsync(userId, notification);
            
            // Send system message
            var body = messageBody ?? $"{notification.Title}\n{notification.Body}";
            await messagingService.SendSystemMessageAsync(userId, body, notification.Url);
        }
        catch
        {
            // Don't fail if notification sending fails
            // This is a secondary operation
        }
    }

    /// <summary>
    /// Sends both push notifications and system messages to multiple users
    /// </summary>
    /// <param name="pushService">Push notification service</param>
    /// <param name="messagingService">Messaging service</param>
    /// <param name="userIds">List of target user IDs</param>
    /// <param name="notification">Push notification DTO</param>
    /// <param name="messageBody">Optional custom message body (uses notification body if null)</param>
    /// <returns></returns>
    public static async Task SendNotificationWithMessagesAsync(
        this IPushNotificationService pushService,
        IMessagingService messagingService,
        IEnumerable<string> userIds,
        SendPushNotificationDto notification,
        string? messageBody = null)
    {
        var tasks = userIds.Select(userId => 
            SendNotificationWithMessageAsync(pushService, messagingService, userId, notification, messageBody));
        
        await Task.WhenAll(tasks);
    }
}
