namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for XP (Experience Points) values per activity type.
/// Bound from appsettings.json "Ranking" section.
/// Used to display XP breakdown in leaderboard details.
/// </summary>
public class XpSettings
{
    public const string SectionName = "Ranking";
    
    /// <summary>
    /// XP awarded per rehearsal attended
    /// </summary>
    public int XpPerRehearsal { get; set; } = 10;
    
    /// <summary>
    /// XP awarded per event type (Festival, Atuacao, Convivio, etc.)
    /// Key is the EventType enum name as string
    /// </summary>
    public Dictionary<string, int> XpPerEventType { get; set; } = new();
    
    /// <summary>
    /// Get XP for a specific event type by name, or 0 if not configured
    /// </summary>
    public int GetXpForEventType(string eventTypeName)
    {
        return XpPerEventType.TryGetValue(eventTypeName, out var xp) ? xp : 0;
    }
}
