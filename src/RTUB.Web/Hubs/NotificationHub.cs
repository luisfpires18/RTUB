using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RTUB.Web.Hubs;

/// <summary>
/// SignalR Hub for real-time browser notifications
/// Used to push notifications to connected clients
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>
    /// Send a notification about a new event to all connected clients
    /// </summary>
    public async Task SendNewEventNotification(string eventName, string eventDate, int eventId)
    {
        await Clients.All.SendAsync("ReceiveNewEventNotification", eventName, eventDate, eventId);
    }
}
