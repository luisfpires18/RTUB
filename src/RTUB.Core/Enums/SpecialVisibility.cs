namespace RTUB.Core.Enums;

/// <summary>
/// Special visibility levels for folders with restricted access
/// </summary>
public enum SpecialVisibility
{
    /// <summary>
    /// No special visibility restrictions
    /// </summary>
    None = 0,

    /// <summary>
    /// Visible only to Direção (Board of Directors)
    /// </summary>
    Direcao = 1,

    /// <summary>
    /// Visible only to Assembleia Geral (General Assembly)
    /// </summary>
    AssembleiaGeral = 2,

    /// <summary>
    /// Visible only to Conselho Fiscal (Fiscal Council)
    /// </summary>
    ConselhoFiscal = 3,

    /// <summary>
    /// Visible only to Tesouraria (Treasury)
    /// </summary>
    Tesouraria = 4,

    /// <summary>
    /// Visible only to Veteranos (Veterans)
    /// </summary>
    Veteranos = 5
}
