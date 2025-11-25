using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Message entity
/// </summary>
public interface IMessageRepository : IRepository<Message>
{
    /// <summary>
    /// Gets messages for a conversation
    /// </summary>
    Task<IEnumerable<Message>> GetConversationMessagesAsync(int conversationId, int? limit = null, int? offset = null);

    /// <summary>
    /// Gets the count of unread messages for a user across all conversations
    /// </summary>
    Task<int> GetUnreadCountForUserAsync(string userId);

    /// <summary>
    /// Gets the count of unread messages in a specific conversation for a user
    /// </summary>
    Task<int> GetUnreadCountForConversationAsync(int conversationId, string userId);

    /// <summary>
    /// Marks all messages in a conversation as read by a user
    /// </summary>
    Task MarkConversationAsReadAsync(int conversationId, string userId);

    /// <summary>
    /// Gets the latest message in a conversation
    /// </summary>
    Task<Message?> GetLatestMessageAsync(int conversationId);
}
