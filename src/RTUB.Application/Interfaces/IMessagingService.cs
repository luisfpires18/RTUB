using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>Messaging v2 (task 041): the React /messages. Every call is decided from the session.</summary>
public interface IMessagingService
{
    Task<EventResult<IReadOnlyList<ConversationListItem>>> GetConversationsAsync(ClaimsPrincipal user);
    Task<EventResult<IReadOnlyList<MessagingPerson>>> GetRecipientsAsync(ClaimsPrincipal user);
    Task<EventResult<ConversationDetail>> GetConversationAsync(int id, int? beforeMessageId, ClaimsPrincipal user);
    Task<EventResult<ConversationDetail>> StartConversationAsync(StartConversationRequest input, ClaimsPrincipal user);
    Task<EventResult<MessageItem>> SendAsync(int conversationId, SendMessageRequest input, ClaimsPrincipal user);
    Task<EventResult<bool>> MarkReadAsync(int conversationId, ClaimsPrincipal user);
    Task<EventResult<ConversationSettings>> SetMutedAsync(int conversationId, bool muted, ClaimsPrincipal user);
    Task<EventResult<MessageItem>> ReactAsync(int messageId, ReactionRequest input, ClaimsPrincipal user);
    Task<EventResult<MessageItem>> RemoveReactionAsync(int messageId, ClaimsPrincipal user);
}
