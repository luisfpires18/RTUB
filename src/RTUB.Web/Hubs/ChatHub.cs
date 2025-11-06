using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using RTUB.Application.Interfaces;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Hubs;

/// <summary>
/// SignalR Hub for real-time chat in event enrollment pages
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Join a specific event's chat group
    /// </summary>
    public async Task JoinEventGroup(int eventId)
    {
        var groupName = $"event-{eventId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave a specific event's chat group
    /// </summary>
    public async Task LeaveEventGroup(int eventId)
    {
        var groupName = $"event-{eventId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Send a message to an event's chat group
    /// </summary>
    public async Task SendMessage(int eventId, string message)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("User not authenticated");
        }

        try
        {
            // Save message to database
            var chatMessage = await _chatService.CreateMessageAsync(eventId, userId, message);
            
            // Send to all clients in the event group
            var groupName = $"event-{eventId}";
            await Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                id = chatMessage.Id,
                userId = chatMessage.UserId,
                userName = chatMessage.User?.GetDisplayName() ?? "Unknown",
                avatarUrl = chatMessage.User?.ProfilePictureSrc ?? "/images/default-avatar.png",
                message = chatMessage.Message,
                sentAt = chatMessage.SentAt
            });
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    /// <summary>
    /// Delete a message (admin only)
    /// </summary>
    [Authorize(Roles = "Admin,Owner")]
    public async Task DeleteMessage(int eventId, int messageId)
    {
        await _chatService.DeleteMessageAsync(messageId);
        
        // Notify all clients in the event group
        var groupName = $"event-{eventId}";
        await Clients.Group(groupName).SendAsync("MessageDeleted", messageId);
    }
}
