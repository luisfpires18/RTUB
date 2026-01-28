using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for calculating event statistics (trophy statistics)
/// Extracted from Events.razor to improve separation of concerns
/// </summary>
public interface IEventStatisticsService
{
    /// <summary>
    /// Gets trophy statistics grouped by event for all past festival events
    /// </summary>
    /// <param name="maxEvents">Maximum number of past events to include (default: 100)</param>
    /// <returns>List of trophy statistics grouped by event, ordered by trophy count descending, then by event date descending</returns>
    Task<List<TrophyStatsByEvent>> GetTrophyStatisticsByEventAsync(int maxEvents = 100);
}
