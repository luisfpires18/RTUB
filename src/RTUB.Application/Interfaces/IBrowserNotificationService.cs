namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing browser push notifications
/// </summary>
public interface IBrowserNotificationService
{
    /// <summary>
    /// Send a browser notification for a new event to subscribed users
    /// </summary>
    /// <param name="eventId">ID of the event</param>
    /// <param name="eventName">Name of the event</param>
    /// <param name="eventDate">Date of the event</param>
    /// <returns>Task with success status and count of notifications sent</returns>
    Task<(bool success, int count)> SendNewEventNotificationAsync(int eventId, string eventName, DateTime eventDate);
}
