using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for InstrumentType display and conversion
/// Centralizes the logic for instrument type display names and parsing
/// </summary>
public static class InstrumentTypeHelper
{
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
            _ => instrument.ToString()
        };
    }

    /// <summary>
    /// Gets the InstrumentType enum value from a localized display name.
    /// Returns null if the display name is not recognized.
    /// </summary>
    public static InstrumentType? ParseDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        // Try each enum value and compare with its display name
        foreach (InstrumentType type in Enum.GetValues(typeof(InstrumentType)))
        {
            if (GetDisplayName(type).Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
    }
}
