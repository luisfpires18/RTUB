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

    /// <summary>
    /// Converts an InstrumentType to its corresponding InventoryItemType (instrument part).
    /// </summary>
    public static InventoryItemType ToInventoryPartType(InstrumentType instrument)
    {
        return instrument switch
        {
            InstrumentType.Guitarra => InventoryItemType.GuitarraPart,
            InstrumentType.Bandolim => InventoryItemType.BandolimPart,
            InstrumentType.Cavaquinho => InventoryItemType.CavaquinhoPart,
            InstrumentType.Acordeao => InventoryItemType.AcordeaoPart,
            InstrumentType.Fagote => InventoryItemType.FagotePart,
            InstrumentType.Flauta => InventoryItemType.FlautaPart,
            InstrumentType.Baixo => InventoryItemType.BaixoPart,
            InstrumentType.Contrabaixo => InventoryItemType.ContrabaixoPart,
            InstrumentType.Percussao => InventoryItemType.PercussaoPart,
            InstrumentType.Pandeireta => InventoryItemType.PandeiretaPart,
            InstrumentType.Estandarte => InventoryItemType.EstandartePart,
            InstrumentType.Violino => InventoryItemType.ViolinoPart,
            InstrumentType.Saxofone => InventoryItemType.SaxofonePart,
            _ => throw new ArgumentOutOfRangeException(nameof(instrument), instrument, "Unknown instrument type")
        };
    }

    /// <summary>
    /// Converts an InventoryItemType (instrument part) back to its InstrumentType.
    /// Returns null if the item type is not an instrument part.
    /// </summary>
    public static InstrumentType? FromInventoryPartType(InventoryItemType itemType)
    {
        return itemType switch
        {
            InventoryItemType.GuitarraPart => InstrumentType.Guitarra,
            InventoryItemType.BandolimPart => InstrumentType.Bandolim,
            InventoryItemType.CavaquinhoPart => InstrumentType.Cavaquinho,
            InventoryItemType.AcordeaoPart => InstrumentType.Acordeao,
            InventoryItemType.FagotePart => InstrumentType.Fagote,
            InventoryItemType.FlautaPart => InstrumentType.Flauta,
            InventoryItemType.BaixoPart => InstrumentType.Baixo,
            InventoryItemType.ContrabaixoPart => InstrumentType.Contrabaixo,
            InventoryItemType.PercussaoPart => InstrumentType.Percussao,
            InventoryItemType.PandeiretaPart => InstrumentType.Pandeireta,
            InventoryItemType.EstandartePart => InstrumentType.Estandarte,
            InventoryItemType.ViolinoPart => InstrumentType.Violino,
            InventoryItemType.SaxofonePart => InstrumentType.Saxofone,
            _ => null
        };
    }

    /// <summary>
    /// Checks if an InventoryItemType is an instrument part.
    /// </summary>
    public static bool IsInstrumentPart(InventoryItemType itemType)
    {
        return (int)itemType >= 100 && (int)itemType <= 112;
    }

    /// <summary>
    /// Instruments removed from the game (no longer drop, cannot forge).
    /// They still exist in the enum/DB for historical data.
    /// </summary>
    public static IReadOnlySet<InstrumentType> RemovedFromGame { get; } = new HashSet<InstrumentType>
    {
        InstrumentType.Baixo,
        InstrumentType.Flauta,
        InstrumentType.Fagote,
        InstrumentType.Saxofone
    };

    /// <summary>
    /// Checks whether an instrument type has been removed from the game.
    /// </summary>
    public static bool IsRemovedFromGame(InstrumentType instrument) => RemovedFromGame.Contains(instrument);

    /// <summary>
    /// Gets all instrument part InventoryItemType values (including removed).
    /// </summary>
    public static IReadOnlyList<InventoryItemType> AllInstrumentPartTypes { get; } =
        Enum.GetValues(typeof(InstrumentType))
            .Cast<InstrumentType>()
            .Select(ToInventoryPartType)
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Gets instrument part InventoryItemType values that are still active in the game.
    /// Excludes removed instruments (Baixo, Flauta, Fagote, Saxofone).
    /// </summary>
    public static IReadOnlyList<InventoryItemType> GameInstrumentPartTypes { get; } =
        Enum.GetValues(typeof(InstrumentType))
            .Cast<InstrumentType>()
            .Where(t => !RemovedFromGame.Contains(t))
            .Select(ToInventoryPartType)
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Gets active (not removed) InstrumentType values.
    /// </summary>
    public static IReadOnlyList<InstrumentType> GameInstrumentTypes { get; } =
        Enum.GetValues(typeof(InstrumentType))
            .Cast<InstrumentType>()
            .Where(t => !RemovedFromGame.Contains(t))
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Pre-cached array of active instrument types for random selection (avoids per-call ToArray).
    /// </summary>
    public static InstrumentType[] GameInstrumentTypesArray { get; } = [.. GameInstrumentTypes];
}
