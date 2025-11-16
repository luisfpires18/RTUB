using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for Position display names
/// Centralizes the logic for position display names in Portuguese
/// </summary>
public static class PositionHelper
{
    /// <summary>
    /// Gets the Portuguese display name for a position
    /// </summary>
    public static string GetDisplayName(Position position)
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
}
