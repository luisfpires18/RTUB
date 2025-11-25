using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for ConversationUserSettings entity
/// </summary>
public interface IConversationUserSettingsRepository : IRepository<ConversationUserSettings>
{
    /// <summary>
    /// Gets the settings for a specific user and conversation
    /// </summary>
    Task<ConversationUserSettings?> GetByUserAndConversationAsync(string userId, int conversationId);

    /// <summary>
    /// Gets all settings for a user across all their conversations
    /// </summary>
    Task<IEnumerable<ConversationUserSettings>> GetByUserAsync(string userId);

    /// <summary>
    /// Gets or creates settings for a user and conversation
    /// </summary>
    Task<ConversationUserSettings> GetOrCreateAsync(string userId, int conversationId);

    /// <summary>
    /// Checks if a conversation is muted for a specific user
    /// </summary>
    Task<bool> IsConversationMutedAsync(string userId, int conversationId);

    /// <summary>
    /// Checks if a conversation is pinned for a specific user
    /// </summary>
    Task<bool> IsConversationPinnedAsync(string userId, int conversationId);
}
