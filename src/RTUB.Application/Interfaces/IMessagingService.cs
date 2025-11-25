using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for managing conversations and messages
/// </summary>
public interface IMessagingService
{
    /// <summary>
    /// Gets all conversations for a user
    /// </summary>
    Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId);

    /// <summary>
    /// Gets a conversation with its messages
    /// </summary>
    Task<ConversationDto?> GetConversationAsync(int conversationId, string currentUserId);

    /// <summary>
    /// Gets messages for a conversation
    /// </summary>
    Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(int conversationId, string currentUserId, int? limit = null);

    /// <summary>
    /// Sends a direct message from one user to another
    /// </summary>
    Task<MessageDto> SendDirectMessageAsync(string senderId, SendMessageDto messageDto);

    /// <summary>
    /// Sends a system message to a user
    /// </summary>
    Task<MessageDto> SendSystemMessageAsync(string receiverId, string body, string? link = null);

    /// <summary>
    /// Marks a conversation as read for a user
    /// </summary>
    Task MarkConversationAsReadAsync(int conversationId, string userId);

    /// <summary>
    /// Marks a conversation as unread for a user (marks the last message as unread)
    /// </summary>
    Task MarkConversationAsUnreadAsync(int conversationId, string userId);

    /// <summary>
    /// Deletes (archives) a conversation for a user
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if the conversation was successfully deleted, false if deletion is not allowed</returns>
    Task<bool> DeleteConversationAsync(int conversationId, string userId);

    /// <summary>
    /// Gets the total unread message count for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Gets or creates a one-to-one conversation between two users
    /// </summary>
    Task<ConversationDto> GetOrCreateConversationAsync(string userId1, string userId2);
    
    /// <summary>
    /// Creates a new group conversation
    /// </summary>
    Task<ConversationDto> CreateGroupConversationAsync(string creatorUserId, string groupName, List<string> participantIds);
    
    /// <summary>
    /// Sends a message to a group conversation
    /// </summary>
    Task<MessageDto> SendGroupMessageAsync(string senderId, int conversationId, string body);
    
    /// <summary>
    /// Gets or creates a system group conversation by title
    /// </summary>
    Task<ConversationDto> GetOrCreateSystemGroupAsync(string groupTitle, List<string> participantIds);
    
    /// <summary>
    /// Updates participants in a group conversation
    /// </summary>
    Task UpdateGroupParticipantsAsync(int conversationId, List<string> participantIds);
    
    /// <summary>
    /// Toggles the mute status of a conversation for a user
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>The new mute status (true if now muted)</returns>
    Task<bool> ToggleMuteAsync(int conversationId, string userId);
    
    /// <summary>
    /// Toggles the pin status of a conversation for a user
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>The new pin status (true if now pinned)</returns>
    Task<bool> TogglePinAsync(int conversationId, string userId);
    
    /// <summary>
    /// Checks if a conversation is muted for a specific user
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if the conversation is muted</returns>
    Task<bool> IsConversationMutedAsync(int conversationId, string userId);
    
    /// <summary>
    /// Checks if a user can send messages in a conversation
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if the user can send messages</returns>
    Task<bool> CanUserSendMessageAsync(int conversationId, string userId);
    
    /// <summary>
    /// Gets or creates an announcement-only system group conversation by title
    /// </summary>
    /// <param name="groupTitle">The title of the group</param>
    /// <param name="participantIds">The list of participant IDs</param>
    /// <returns>The conversation DTO</returns>
    Task<ConversationDto> GetOrCreateAnnouncementGroupAsync(string groupTitle, List<string> participantIds);
}
