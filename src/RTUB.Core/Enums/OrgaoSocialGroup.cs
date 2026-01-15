namespace RTUB.Core.Enums;

/// <summary>
/// Groups of Orgãos Sociais (organizational bodies)
/// Used for categorizing positions when assigning questions
/// </summary>
public enum OrgaoSocialGroup
{
    /// <summary>
    /// Direção - Board of Directors (Magister, ViceMagister, Secretario, Tesoureiros)
    /// </summary>
    Direcao,

    /// <summary>
    /// Conselho Fiscal - Fiscal Council (Presidente, Relatores)
    /// </summary>
    ConselhoFiscal,

    /// <summary>
    /// Assembleia Geral - General Assembly (Presidente, Secretários)
    /// </summary>
    AssembleiaGeral,

    /// <summary>
    /// Ensaiador - Musical Director
    /// </summary>
    Ensaiador,

    /// <summary>
    /// Conselho de Veteranos - Veterans Council (Presidente)
    /// </summary>
    ConselhoVeteranos
}
