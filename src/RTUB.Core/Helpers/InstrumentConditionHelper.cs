using RTUB.Core.Enums;

namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for InstrumentCondition display and styling
/// Centralizes the logic for condition display names and badge classes
/// </summary>
public static class InstrumentConditionHelper
{
    /// <summary>
    /// Cached mapping from InstrumentCondition to display name for O(1) lookup
    /// </summary>
    private static readonly Dictionary<InstrumentCondition, string> ConditionToDisplayName = BuildConditionDisplayLookup();

    /// <summary>
    /// Cached mapping from InstrumentCondition to badge class for O(1) lookup
    /// </summary>
    private static readonly Dictionary<InstrumentCondition, string> ConditionToBadgeClass = BuildConditionBadgeLookup();

    private static Dictionary<InstrumentCondition, string> BuildConditionDisplayLookup()
    {
        var lookup = new Dictionary<InstrumentCondition, string>();
        foreach (InstrumentCondition condition in Enum.GetValues<InstrumentCondition>())
        {
            lookup[condition] = GetDisplayNameInternal(condition);
        }
        return lookup;
    }

    private static Dictionary<InstrumentCondition, string> BuildConditionBadgeLookup()
    {
        var lookup = new Dictionary<InstrumentCondition, string>();
        foreach (InstrumentCondition condition in Enum.GetValues<InstrumentCondition>())
        {
            lookup[condition] = GetBadgeClassInternal(condition);
        }
        return lookup;
    }

    private static string GetDisplayNameInternal(InstrumentCondition condition)
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

    private static string GetBadgeClassInternal(InstrumentCondition condition)
    {
        return condition switch
        {
            InstrumentCondition.Excellent => "bg-success",      // Green
            InstrumentCondition.Good => "bg-info",              // Blue
            InstrumentCondition.Worn => "bg-orange",            // Orange
            InstrumentCondition.NeedsMaintenance => "bg-warning", // Yellow
            InstrumentCondition.Lost => "bg-danger",            // Red
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Gets the Portuguese display name for an instrument condition
    /// Uses cached dictionary for O(1) lookup performance
    /// </summary>
    public static string GetDisplayName(InstrumentCondition condition)
    {
        return ConditionToDisplayName.TryGetValue(condition, out var displayName)
            ? displayName
            : condition.ToString();
    }

    /// <summary>
    /// Gets the Bootstrap badge CSS class for an instrument condition
    /// Uses cached dictionary for O(1) lookup performance
    /// </summary>
    public static string GetBadgeClass(InstrumentCondition condition)
    {
        return ConditionToBadgeClass.TryGetValue(condition, out var badgeClass)
            ? badgeClass
            : "bg-secondary";
    }

}
