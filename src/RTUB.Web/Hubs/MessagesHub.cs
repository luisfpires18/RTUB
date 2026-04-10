using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Web.Services;

namespace RTUB.Web.Hubs;

/// <summary>
/// SignalR hub for real-time messaging functionality
/// </summary>
[Authorize]
public class MessagesHub : Hub<IMessagesHubClient>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessagingService _messagingService;
    private readonly MessagesNotificationService _notificationService;
    private readonly ILogger<MessagesHub> _logger;

    // In-memory cache: conversationId → set of validated participant userIds.
    // Populated on JoinConversation (which already does the DB check).
    // Avoids a round-trip per typing event.
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _participantCache = new();

    public MessagesHub(
        IConversationRepository conversationRepository,
        IMessagingService messagingService,
        MessagesNotificationService notificationService,
        ILogger<MessagesHub> logger)
    {
        _conversationRepository = conversationRepository;
        _messagingService = messagingService;
        _notificationService = notificationService;
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

        // Cache this user's membership so typing events don't need a DB lookup
        var participants = _participantCache.GetOrAdd(conversationId, _ => new ConcurrentDictionary<string, byte>());
        participants.TryAdd(userId, 0);

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

        // Validate from cache first (populated on JoinConversation)
        if (!_participantCache.TryGetValue(conversationId, out var participants) || !participants.ContainsKey(userId))
        {
            // Cache miss — fall back to DB validation
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null || !conversation.HasParticipant(userId))
            {
                return;
            }
            // Populate cache for future calls
            var p = _participantCache.GetOrAdd(conversationId, _ => new ConcurrentDictionary<string, byte>());
            p.TryAdd(userId, 0);
        }

        var groupName = $"conversation-{conversationId}";
        // Send to all in group except the sender
        await Clients.OthersInGroup(groupName).TypingStarted(conversationId, userId);

        // Also notify server-side Blazor components
        await _notificationService.NotifyTypingStartedAsync(conversationId, userId);
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

        // Also notify server-side Blazor components
        await _notificationService.NotifyTypingStoppedAsync(conversationId, userId);
    }

    /// <summary>
    /// Toggles an emoji reaction on a message and broadcasts the update to all participants
    /// </summary>
    public async Task SendReaction(int conversationId, int messageId, string emoji)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        // Validate participation via cache or DB
        if (!_participantCache.TryGetValue(conversationId, out var participants) || !participants.ContainsKey(userId))
        {
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null || !conversation.HasParticipant(userId)) return;
        }

        try
        {
            var updatedReactions = await _messagingService.ToggleReactionAsync(messageId, userId, emoji);

            // Broadcast to all in the conversation group (including sender)
            var groupName = $"conversation-{conversationId}";
            await Clients.Group(groupName).ReactionUpdated(conversationId, messageId, updatedReactions);

            // Notify server-side Blazor components
            await _notificationService.NotifyReactionUpdatedAsync(conversationId, messageId, updatedReactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reaction for message {MessageId} in conversation {ConversationId}",
                messageId, conversationId);
        }
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
