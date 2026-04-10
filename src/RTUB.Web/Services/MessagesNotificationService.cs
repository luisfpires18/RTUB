using RTUB.Application.DTOs;

namespace RTUB.Web.Services;

/// <summary>
/// Service for notifying components about real-time messaging events in Blazor Server.
/// This replaces the client-side SignalR connection which doesn't work in server-side Blazor.
///
/// IMPORTANT: Because this is a singleton shared across multiple Blazor circuits,
/// each event may have multiple subscribers (e.g. two UnreadMessagesBadge instances
/// plus the Inbox page). The standard multicast Func&lt;..., Task&gt;.Invoke() only
/// returns the Task of the LAST subscriber, silently discarding earlier ones.
/// We use GetInvocationList() + Task.WhenAll() to await every subscriber properly.
/// </summary>
public class MessagesNotificationService
{
    // Events for components to subscribe to
    public event Func<MessageDto, Task>? OnMessageReceived;
    public event Func<int, string, DateTime, Task>? OnMessageSeen;
    public event Func<int, string, Task>? OnTypingStarted;
    public event Func<int, string, Task>? OnTypingStopped;
    public event Func<int, int, List<MessageReactionSummaryDto>, Task>? OnReactionUpdated;

    /// <summary>
    /// Notifies subscribers that a new message was received
    /// </summary>
    public async Task NotifyMessageReceivedAsync(MessageDto message)
    {
        var handler = OnMessageReceived;
        if (handler != null)
        {
            await InvokeAllAsync(handler.GetInvocationList(), d =>
                ((Func<MessageDto, Task>)d)(message));
        }
    }

    /// <summary>
    /// Notifies subscribers that messages were marked as seen
    /// </summary>
    public async Task NotifyMessageSeenAsync(int conversationId, string userId, DateTime seenAt)
    {
        var handler = OnMessageSeen;
        if (handler != null)
        {
            await InvokeAllAsync(handler.GetInvocationList(), d =>
                ((Func<int, string, DateTime, Task>)d)(conversationId, userId, seenAt));
        }
    }

    /// <summary>
    /// Notifies subscribers that a user started typing
    /// </summary>
    public async Task NotifyTypingStartedAsync(int conversationId, string userId)
    {
        var handler = OnTypingStarted;
        if (handler != null)
        {
            await InvokeAllAsync(handler.GetInvocationList(), d =>
                ((Func<int, string, Task>)d)(conversationId, userId));
        }
    }

    /// <summary>
    /// Notifies subscribers that a user stopped typing
    /// </summary>
    public async Task NotifyTypingStoppedAsync(int conversationId, string userId)
    {
        var handler = OnTypingStopped;
        if (handler != null)
        {
            await InvokeAllAsync(handler.GetInvocationList(), d =>
                ((Func<int, string, Task>)d)(conversationId, userId));
        }
    }

    /// <summary>
    /// Notifies subscribers that reactions on a message were updated
    /// </summary>
    public async Task NotifyReactionUpdatedAsync(int conversationId, int messageId, List<MessageReactionSummaryDto> reactions)
    {
        var handler = OnReactionUpdated;
        if (handler != null)
        {
            await InvokeAllAsync(handler.GetInvocationList(), d =>
                ((Func<int, int, List<MessageReactionSummaryDto>, Task>)d)(conversationId, messageId, reactions));
        }
    }

    /// <summary>
    /// Invokes every subscriber in the multicast delegate's invocation list
    /// and awaits all of them concurrently. Individual subscriber failures are
    /// caught so one broken circuit doesn't prevent other subscribers from updating.
    /// </summary>
    private static async Task InvokeAllAsync(Delegate[] delegates, Func<Delegate, Task> invoker)
    {
        var tasks = new Task[delegates.Length];
        for (var i = 0; i < delegates.Length; i++)
        {
            try
            {
                tasks[i] = invoker(delegates[i]);
            }
            catch (Exception)
            {
                tasks[i] = Task.CompletedTask;
            }
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Individual subscriber errors are non-fatal.
            // Each subscriber already handles its own exceptions.
        }
    }
}
