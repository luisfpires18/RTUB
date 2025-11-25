using Microsoft.AspNetCore.SignalR;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Web.Hubs;

namespace RTUB.Web.Services;

/// <summary>
/// Service for broadcasting messaging events through SignalR hub
/// </summary>
public class MessagesHubService : IMessagesHubService
{
    private readonly IHubContext<MessagesHub, IMessagesHubClient> _hubContext;
    private readonly ILogger<MessagesHubService> _logger;

    public MessagesHubService(
        IHubContext<MessagesHub, IMessagesHubClient> hubContext,
        ILogger<MessagesHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastMessageAsync(int conversationId, MessageDto message)
    {
        try
        {
            var groupName = $"conversation-{conversationId}";
            await _hubContext.Clients.Group(groupName).ReceiveMessage(message);
            
            _logger.LogDebug("Broadcasted message {MessageId} to conversation {ConversationId}", 
                message.Id, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting message {MessageId} to conversation {ConversationId}", 
                message.Id, conversationId);
        }
    }

    public async Task NotifyMessageSeenAsync(int conversationId, string userId, DateTime seenAt)
    {
        try
        {
            var groupName = $"conversation-{conversationId}";
            await _hubContext.Clients.Group(groupName).MessageSeen(conversationId, userId, seenAt);
            
            _logger.LogDebug("Notified conversation {ConversationId} that user {UserId} marked messages as seen", 
                conversationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying message seen for conversation {ConversationId}", 
                conversationId);
        }
    }

    public async Task NotifyTypingStartedAsync(int conversationId, string userId)
    {
        try
        {
            var groupName = $"conversation-{conversationId}";
            await _hubContext.Clients.Group(groupName).TypingStarted(conversationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying typing started for conversation {ConversationId}", 
                conversationId);
        }
    }

    public async Task NotifyTypingStoppedAsync(int conversationId, string userId)
    {
        try
        {
            var groupName = $"conversation-{conversationId}";
            await _hubContext.Clients.Group(groupName).TypingStopped(conversationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying typing stopped for conversation {ConversationId}", 
                conversationId);
        }
    }
}
