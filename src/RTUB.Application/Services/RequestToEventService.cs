using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for creating events from requests
/// Extracted from Requests.razor to improve separation of concerns
/// </summary>
public class RequestToEventService : IRequestToEventService
{
    /// <summary>
    /// Creates navigation parameters for creating an event from a request
    /// </summary>
    /// <param name="request">The request to create event from</param>
    /// <returns>Dictionary of query parameters for navigation</returns>
    public Dictionary<string, object?> CreateEventNavigationParameters(Request request)
    {
        // Build event name: "EventType em Location"
        var eventName = $"{request.EventType} em {request.Location}";
        
        // Build description: "Pedido de Name"
        var description = $"Pedido de {request.Name}";
        
        // Return query parameters for navigation
        return new Dictionary<string, object?>
        {
            ["openModal"] = "true",
            ["name"] = eventName,
            ["location"] = request.Location,
            ["date"] = request.PreferredDate.ToString("yyyy-MM-dd"),
            ["description"] = description,
            ["type"] = "Atuacao", // Default event type
            ["requestId"] = request.Id
        };
    }
}
