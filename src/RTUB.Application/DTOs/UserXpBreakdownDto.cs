namespace RTUB.Application.DTOs;

/// <summary>
/// DTO containing a user's XP breakdown by activity type
/// Shows how XP is distributed across rehearsals and event types
/// </summary>
public class UserXpBreakdownDto
{
    /// <summary>
    /// Total XP earned by the user
    /// </summary>
    public int TotalXp { get; set; }
    
    /// <summary>
    /// Number of rehearsals attended
    /// </summary>
    public int RehearsalCount { get; set; }
    
    /// <summary>
    /// XP awarded per rehearsal (from configuration)
    /// </summary>
    public int RehearsalXpPerUnit { get; set; }
    
    /// <summary>
    /// Total XP earned from rehearsals = RehearsalCount × RehearsalXpPerUnit
    /// </summary>
    public int RehearsalXpTotal { get; set; }
    
    /// <summary>
    /// XP breakdown by event type
    /// </summary>
    public List<EventTypeXpDto> EventsByType { get; set; } = new();
}
