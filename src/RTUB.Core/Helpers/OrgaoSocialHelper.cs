using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for Orgãos Sociais group display names and position mappings
/// </summary>
public static class OrgaoSocialHelper
{
    /// <summary>
    /// Gets the Portuguese display name for an Orgão Social group
    /// </summary>
    public static string GetDisplayName(OrgaoSocialGroup group)
    {
        return group switch
        {
            OrgaoSocialGroup.Direcao => "Direção",
            OrgaoSocialGroup.ConselhoFiscal => "Conselho Fiscal",
            OrgaoSocialGroup.AssembleiaGeral => "Assembleia Geral",
            OrgaoSocialGroup.Ensaiador => "Ensaiador",
            OrgaoSocialGroup.ConselhoVeteranos => "Conselho de Veteranos",
            _ => group.ToString()
        };
    }

    /// <summary>
    /// Gets the positions that belong to an Orgão Social group
    /// </summary>
    public static IEnumerable<Position> GetPositionsForGroup(OrgaoSocialGroup group)
    {
        return group switch
        {
            OrgaoSocialGroup.Direcao => new[]
            {
                Position.Magister,
                Position.ViceMagister,
                Position.Secretario,
                Position.PrimeiroTesoureiro,
                Position.SegundoTesoureiro
            },
            OrgaoSocialGroup.ConselhoFiscal => new[]
            {
                Position.PresidenteConselhoFiscal,
                Position.PrimeiroRelatorConselhoFiscal,
                Position.SegundoRelatorConselhoFiscal
            },
            OrgaoSocialGroup.AssembleiaGeral => new[]
            {
                Position.PresidenteMesaAssembleia,
                Position.PrimeiroSecretarioMesaAssembleia,
                Position.SegundoSecretarioMesaAssembleia
            },
            OrgaoSocialGroup.Ensaiador => new[]
            {
                Position.Ensaiador
            },
            OrgaoSocialGroup.ConselhoVeteranos => new[]
            {
                Position.PresidenteConselhoVeteranos
            },
            _ => Enumerable.Empty<Position>()
        };
    }

    /// <summary>
    /// Gets all Orgão Social groups as an enumerable
    /// </summary>
    public static IEnumerable<OrgaoSocialGroup> GetAllGroups()
    {
        return Enum.GetValues<OrgaoSocialGroup>();
    }

    /// <summary>
    /// Gets the Orgão Social group for a given position
    /// </summary>
    public static OrgaoSocialGroup? GetGroupForPosition(Position position)
    {
        foreach (var group in GetAllGroups())
        {
            if (GetPositionsForGroup(group).Contains(position))
            {
                return group;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all positions from all Orgãos Sociais
    /// </summary>
    public static IEnumerable<Position> GetAllOrgaoSocialPositions()
    {
        return GetAllGroups().SelectMany(GetPositionsForGroup).Distinct();
    }
}
