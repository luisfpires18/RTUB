using Microsoft.AspNetCore.SignalR;
using RTUB.Application.Interfaces;
using RTUB.Web.Hubs;
using System.Globalization;

namespace RTUB.Web.Services;

/// <summary>
/// Web layer notification service that handles SignalR broadcasting
/// Wraps the Application layer BrowserNotificationService
/// </summary>
public class WebBrowserNotificationService
{
    private readonly IBrowserNotificationService _browserNotificationService;
    private readonly IHubContext<NotificationHub> _hubContext;

    public WebBrowserNotificationService(
        IBrowserNotificationService browserNotificationService,
        IHubContext<NotificationHub> hubContext)
    {
        _browserNotificationService = browserNotificationService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Send a browser notification for a new event and broadcast via SignalR
    /// </summary>
    public async Task<(bool success, int count)> SendNewEventNotificationAsync(
        int eventId,
        string eventName,
        DateTime eventDate)
    {
        // First check who should receive the notification
        var (success, count) = await _browserNotificationService.SendNewEventNotificationAsync(
            eventId, eventName, eventDate);

        if (success && count > 0)
        {
            // Format the event date in Portuguese
            var formattedDate = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy • HH:mm",
                new CultureInfo("pt-PT"));

            // Broadcast via SignalR to all connected clients
            await _hubContext.Clients.All.SendAsync(
                "ReceiveNewEventNotification",
                eventName,
                formattedDate,
                eventId);
        }

        return (success, count);
    }
}
