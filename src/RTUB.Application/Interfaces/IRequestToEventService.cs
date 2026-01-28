using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for creating events from requests
/// Extracted from Requests.razor to improve separation of concerns
/// </summary>
public interface IRequestToEventService
{
    /// <summary>
    /// Creates navigation parameters for creating an event from a request
    /// </summary>
    /// <param name="request">The request to create event from</param>
    /// <returns>Dictionary of query parameters for navigation</returns>
    Dictionary<string, object?> CreateEventNavigationParameters(Request request);
}
