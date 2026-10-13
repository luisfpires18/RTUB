using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for InstrumentType display and conversion
/// Centralizes the logic for instrument type display names and parsing
/// </summary>
public static class InstrumentTypeHelper
{
    /// <summary>
    /// Cached mapping from display name (lowercase) to InstrumentType for O(1) lookup
    /// </summary>
    private static readonly Dictionary<string, InstrumentType> DisplayNameToType = BuildDisplayNameLookup();

    private static Dictionary<string, InstrumentType> BuildDisplayNameLookup()
    {
        var lookup = new Dictionary<string, InstrumentType>(StringComparer.OrdinalIgnoreCase);
        foreach (InstrumentType type in Enum.GetValues(typeof(InstrumentType)))
        {
            var displayName = GetDisplayName(type);
            lookup[displayName] = type;
        }
        return lookup;
    }

    /// <summary>
    /// Gets a localized display name for an instrument type.
    /// </summary>
    public static string GetDisplayName(InstrumentType instrument)
    {
        return instrument switch
        {
            InstrumentType.Guitarra => "Guitarra",
            InstrumentType.Bandolim => "Bandolim",
            InstrumentType.Cavaquinho => "Cavaquinho",
            InstrumentType.Acordeao => "Acordeão",
            InstrumentType.Fagote => "Fagote",
            InstrumentType.Flauta => "Flauta",
            InstrumentType.Baixo => "Baixo",
            InstrumentType.Contrabaixo => "Contrabaixo",
            InstrumentType.Percussao => "Percussão",
            InstrumentType.Pandeireta => "Pandeireta",
            InstrumentType.Estandarte => "Estandarte",
            InstrumentType.Violino => "Violino",
            InstrumentType.Saxofone => "Saxofone",
            _ => instrument.ToString()
        };
    }

    /// <summary>
    /// Gets the InstrumentType enum value from a localized display name.
    /// Returns null if the display name is not recognized.
    /// Uses cached dictionary for O(1) lookup performance.
    /// </summary>
    public static InstrumentType? ParseDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        return DisplayNameToType.TryGetValue(displayName, out var type) ? type : null;
    }
}
