using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Conversation entity
/// </summary>
public interface IConversationRepository : IRepository<Conversation>
{
    /// <summary>
    /// Gets all conversations for a user that are not archived
    /// </summary>
    Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId, bool includeArchived = false);

    /// <summary>
    /// Gets a conversation with its messages loaded
    /// </summary>
    Task<Conversation?> GetWithMessagesAsync(int conversationId, int? limit = null);

    /// <summary>
    /// Gets or creates a one-to-one conversation between two users
    /// </summary>
    Task<Conversation> GetOrCreateOneToOneAsync(string userId1, string userId2);

    /// <summary>
    /// Gets a conversation by its participants
    /// </summary>
    Task<Conversation?> GetByParticipantsAsync(List<string> participantIds);

    /// <summary>
    /// Gets the system conversation for a specific user (used for system notifications)
    /// </summary>
    Task<Conversation?> GetSystemConversationForUserAsync(string userId);

    /// <summary>
    /// Archives a conversation for a user (soft delete)
    /// </summary>
    Task ArchiveConversationAsync(int conversationId);

    /// <summary>
    /// Gets a group conversation by its title (for system groups)
    /// </summary>
    Task<Conversation?> GetGroupByTitleAsync(string title);

    /// <summary>
    /// Gets all group conversations created by system
    /// </summary>
    Task<IEnumerable<Conversation>> GetSystemGroupsAsync();
}
