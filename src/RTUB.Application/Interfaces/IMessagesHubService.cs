using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for SignalR hub interactions in the messaging system.
/// Abstracts hub operations for testability and separation of concerns.
/// </summary>
public interface IMessagesHubService
{
    /// <summary>
    /// Broadcasts a new message to all participants in a conversation
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="message">The message to broadcast</param>
    Task BroadcastMessageAsync(int conversationId, MessageDto message);

    /// <summary>
    /// Notifies participants when messages in a conversation are marked as seen
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user who marked the messages as seen</param>
    /// <param name="seenAt">The timestamp when messages were marked as seen</param>
    Task NotifyMessageSeenAsync(int conversationId, string userId, DateTime seenAt);

    /// <summary>
    /// Notifies participants when a user starts typing in a conversation
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user who started typing</param>
    Task NotifyTypingStartedAsync(int conversationId, string userId);

    /// <summary>
    /// Notifies participants when a user stops typing in a conversation
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user who stopped typing</param>
    Task NotifyTypingStoppedAsync(int conversationId, string userId);

    /// <summary>
    /// Broadcasts updated reaction data for a message to all participants in a conversation
    /// </summary>
    Task BroadcastReactionAsync(int conversationId, int messageId, List<MessageReactionSummaryDto> reactions);
}
