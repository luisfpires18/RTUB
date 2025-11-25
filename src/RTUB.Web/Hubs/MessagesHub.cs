using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RTUB.Application.Interfaces;
using RTUB.Application.DTOs;
using System.Security.Claims;

namespace RTUB.Web.Hubs;

/// <summary>
/// SignalR hub for real-time messaging functionality
/// </summary>
[Authorize]
public class MessagesHub : Hub<IMessagesHubClient>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ILogger<MessagesHub> _logger;

    public MessagesHub(
        IConversationRepository conversationRepository,
        ILogger<MessagesHub> logger)
    {
        _conversationRepository = conversationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user's ID from claims
    /// </summary>
    private string? GetCurrentUserId()
    {
        return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Joins a conversation group for real-time updates
    /// </summary>
    /// <param name="conversationId">The conversation ID to join</param>
    public async Task JoinConversation(int conversationId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User attempted to join conversation without valid user ID");
            return;
        }

        // Validate that user is a participant in this conversation
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null)
        {
            _logger.LogWarning("User {UserId} attempted to join non-existent conversation {ConversationId}", 
                userId, conversationId);
            return;
        }

        if (!conversation.HasParticipant(userId))
        {
            _logger.LogWarning("User {UserId} attempted to join conversation {ConversationId} without being a participant", 
                userId, conversationId);
            return;
        }

        var groupName = $"conversation-{conversationId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Leaves a conversation group
    /// </summary>
    /// <param name="conversationId">The conversation ID to leave</param>
    public async Task LeaveConversation(int conversationId)
    {
        var userId = GetCurrentUserId();
        var groupName = $"conversation-{conversationId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug("User {UserId} left conversation {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Notifies other participants that the current user is typing
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    public async Task SendTypingStarted(int conversationId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Validate user is a participant
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null || !conversation.HasParticipant(userId))
        {
            return;
        }

        var groupName = $"conversation-{conversationId}";
        // Send to all in group except the sender
        await Clients.OthersInGroup(groupName).TypingStarted(conversationId, userId);
    }

    /// <summary>
    /// Notifies other participants that the current user stopped typing
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    public async Task SendTypingStopped(int conversationId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var groupName = $"conversation-{conversationId}";
        // Send to all in group except the sender
        await Clients.OthersInGroup(groupName).TypingStopped(conversationId, userId);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        _logger.LogDebug("User {UserId} connected to MessagesHub with ConnectionId {ConnectionId}", 
            userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (exception != null)
        {
            _logger.LogWarning(exception, "User {UserId} disconnected from MessagesHub with error", userId);
        }
        else
        {
            _logger.LogDebug("User {UserId} disconnected from MessagesHub", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

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
