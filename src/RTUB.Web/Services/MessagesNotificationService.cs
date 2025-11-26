using RTUB.Application.DTOs;

namespace RTUB.Web.Services;

/// <summary>
/// Service for notifying components about real-time messaging events in Blazor Server.
/// This replaces the client-side SignalR connection which doesn't work in server-side Blazor.
/// </summary>
public class MessagesNotificationService
{
    // Events for components to subscribe to
    public event Func<MessageDto, Task>? OnMessageReceived;
    public event Func<int, string, DateTime, Task>? OnMessageSeen;
    public event Func<int, string, Task>? OnTypingStarted;
    public event Func<int, string, Task>? OnTypingStopped;

    /// <summary>
    /// Notifies subscribers that a new message was received
    /// </summary>
    public async Task NotifyMessageReceivedAsync(MessageDto message)
    {
        if (OnMessageReceived != null)
        {
            await OnMessageReceived.Invoke(message);
        }
    }

    /// <summary>
    /// Notifies subscribers that messages were marked as seen
    /// </summary>
    public async Task NotifyMessageSeenAsync(int conversationId, string userId, DateTime seenAt)
    {
        if (OnMessageSeen != null)
        {
            await OnMessageSeen.Invoke(conversationId, userId, seenAt);
        }
    }

    /// <summary>
    /// Notifies subscribers that a user started typing
    /// </summary>
    public async Task NotifyTypingStartedAsync(int conversationId, string userId)
    {
        if (OnTypingStarted != null)
        {
            await OnTypingStarted.Invoke(conversationId, userId);
        }
    }

    /// <summary>
    /// Notifies subscribers that a user stopped typing
    /// </summary>
    public async Task NotifyTypingStoppedAsync(int conversationId, string userId)
    {
        if (OnTypingStopped != null)
        {
            await OnTypingStopped.Invoke(conversationId, userId);
        }
    }
}
