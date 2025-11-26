using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for Position display names
/// Centralizes the logic for position display names in Portuguese
/// </summary>
public static class PositionHelper
{
    /// <summary>
    /// Cached mapping from display name (lowercase) to Position for O(1) lookup
    /// </summary>
    private static readonly Dictionary<string, Position> DisplayNameToPosition = BuildDisplayNameLookup();

    /// <summary>
    /// Cached mapping from Position to display name for O(1) lookup
    /// </summary>
    private static readonly Dictionary<Position, string> PositionToDisplayName = BuildPositionLookup();

    private static Dictionary<string, Position> BuildDisplayNameLookup()
    {
        var lookup = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase);
        foreach (Position position in Enum.GetValues<Position>())
        {
            var displayName = GetDisplayNameInternal(position);
            lookup[displayName] = position;
        }
        return lookup;
    }

    private static Dictionary<Position, string> BuildPositionLookup()
    {
        var lookup = new Dictionary<Position, string>();
        foreach (Position position in Enum.GetValues<Position>())
        {
            lookup[position] = GetDisplayNameInternal(position);
        }
        return lookup;
    }

    private static string GetDisplayNameInternal(Position position)
    {
        return position switch
        {
            // Direção
            Position.Magister => "Magister",
            Position.ViceMagister => "Vice-Magister",
            Position.Secretario => "Secretário",
            Position.PrimeiroTesoureiro => "Primeiro Tesoureiro",
            Position.SegundoTesoureiro => "Segundo Tesoureiro",

            // Mesa da Assembleia
            Position.PresidenteMesaAssembleia => "Presidente da Mesa de Assembleia",
            Position.PrimeiroSecretarioMesaAssembleia => "Primeiro Secretário da Mesa de Assembleia",
            Position.SegundoSecretarioMesaAssembleia => "Segundo Secretário da Mesa de Assembleia",

            // Conselho Fiscal
            Position.PresidenteConselhoFiscal => "Presidente do Conselho Fiscal",
            Position.PrimeiroRelatorConselhoFiscal => "Primeiro Relator do Conselho Fiscal",
            Position.SegundoRelatorConselhoFiscal => "Segundo Relator do Conselho Fiscal",

            // Conselho de Veteranos
            Position.PresidenteConselhoVeteranos => "Presidente do Conselho de Veteranos",

            // Outros Cargos
            Position.Ensaiador => "Ensaiador",

            // Fallback - should never happen if all positions are handled
            _ => position.ToString() // Return enum name as last resort
        };
    }

    /// <summary>
    /// Gets the Portuguese display name for a position
    /// Uses cached dictionary for O(1) lookup performance
    /// </summary>
    public static string GetDisplayName(Position position)
    {
        return PositionToDisplayName.TryGetValue(position, out var displayName)
            ? displayName
            : position.ToString();
    }

    /// <summary>
    /// Gets the Position enum value from a localized display name.
    /// Returns null if the display name is not recognized.
    /// Uses cached dictionary for O(1) lookup performance.
    /// </summary>
    public static Position? ParseDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        return DisplayNameToPosition.TryGetValue(displayName, out var position) ? position : null;
    }
}
