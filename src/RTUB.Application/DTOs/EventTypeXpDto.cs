namespace RTUB.Application.DTOs;

/// <summary>
/// DTO representing XP earned from a specific event type
/// </summary>
public class EventTypeXpDto
{
    /// <summary>
    /// Event type name (Festival, Atuacao, Convivio, etc.)
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Number of events of this type attended
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// XP awarded per event of this type (from configuration)
    /// </summary>
    public int XpPerUnit { get; set; }

    /// <summary>
    /// Total XP earned from this event type = Count × XpPerUnit
    /// </summary>
    public int TotalXp { get; set; }
}
