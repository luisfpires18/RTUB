using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for InstrumentCondition display and styling
/// Centralizes the logic for condition display names and badge classes
/// </summary>
public static class InstrumentConditionHelper
{
    /// <summary>
    /// Gets the Portuguese display name for an instrument condition
    /// </summary>
    public static string GetDisplayName(InstrumentCondition condition)
    {
        return condition switch
        {
            InstrumentCondition.Excellent => "Óptimo",
            InstrumentCondition.Good => "Bom",
            InstrumentCondition.Worn => "Velho",
            InstrumentCondition.NeedsMaintenance => "Precisa Manutenção",
            InstrumentCondition.Lost => "Perdido",
            _ => condition.ToString()
        };
    }

    /// <summary>
    /// Gets the Bootstrap badge CSS class for an instrument condition
    /// </summary>
    public static string GetBadgeClass(InstrumentCondition condition)
    {
        return condition switch
        {
            InstrumentCondition.Excellent => "bg-success",      // Green
            InstrumentCondition.Good => "bg-info",              // Blue
            InstrumentCondition.Worn => "bg-warning",           // Orange
            InstrumentCondition.NeedsMaintenance => "bg-warning", // Yellow
            InstrumentCondition.Lost => "bg-danger",            // Red
            _ => "bg-secondary"
        };
    }
}
