using RTUB.Application.DTOs;

namespace RTUB.Web.Hubs;

/// <summary>
/// Client-side methods that the hub can invoke
/// </summary>
public interface IMessagesHubClient
{
    /// <summary>
    /// Receives a new message in a conversation
    /// </summary>
    Task ReceiveMessage(MessageDto message);

    /// <summary>
    /// Notified when messages are marked as seen
    /// </summary>
    Task MessageSeen(int conversationId, string userId, DateTime seenAt);

    /// <summary>
    /// Notified when a user starts typing
    /// </summary>
    Task TypingStarted(int conversationId, string userId);

    /// <summary>
    /// Notified when a user stops typing
    /// </summary>
    Task TypingStopped(int conversationId, string userId);
}
