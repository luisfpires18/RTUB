namespace RTUB.Core.Enums;

/// <summary>
/// Physical condition of an instrument asset
/// </summary>
public enum InstrumentCondition
{
    /// <summary>
    /// Óptimo - Excellent condition (Green)
    /// </summary>
    Excellent,
    
    /// <summary>
    /// Bom - Good condition (Blue)
    /// </summary>
    Good,
    
    /// <summary>
    /// Velho - Old/Worn (Orange)
    /// </summary>
    Worn,
    
    /// <summary>
    /// Precisa Manutenção - Needs maintenance (Yellow)
    /// </summary>
    NeedsMaintenance,
    
    /// <summary>
    /// Perdido - Lost (Red)
    /// </summary>
    Lost
}
