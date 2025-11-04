using RTUB.Core.Enums;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for EventType enum formatting
/// </summary>
public static class EventTypeExtensions
{
    /// <summary>
    /// Gets the Portuguese display name for an EventType value
    /// </summary>
    public static string GetDisplayName(this EventType eventType)
    {
        return eventType switch
        {
            EventType.Atuacao => "Atuação",
            EventType.Festival => "Festival",
            EventType.Casamento => "Casamento",
            EventType.Serenata => "Serenata",
            EventType.Arraial => "Arraial",
            EventType.Convivio => "Convívio",
            EventType.Nerba => "NERBA",
            EventType.Missa => "Missa",
            EventType.Batizado => "Batizado",
            _ => eventType.ToString()
        };
    }
}
