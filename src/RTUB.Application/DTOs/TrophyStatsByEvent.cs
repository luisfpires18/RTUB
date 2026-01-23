using RTUB.Core.Entities;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for trophy statistics grouped by event
/// Used in Events page for displaying trophy statistics
/// </summary>
public class TrophyStatsByEvent
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int TrophyCount { get; set; }
    public List<Trophy> Trophies { get; set; } = new();
}
