namespace RTUB.Core.Helpers;

/// <summary>
/// Maps arena rating to a ranked tier (Iron → Challenger).
/// </summary>
public static class RankHelper
{
    /// <summary>
    /// Returns the full rank name (e.g. "Gold II") and its CSS-friendly color for the given rating.
    /// </summary>
    public static (string Name, string Color, string Icon) GetRank(int rating)
    {
        return rating switch
        {
            >= 2700 => ("Challenger", "#f4c542", "bi-star-fill"),
            >= 2400 => ("Grandmaster", "#ff4444", "bi-fire"),
            >= 2100 => ("Master", "#9b59b6", "bi-gem"),
            >= 2000 => ("Diamond I", "#b9f2ff", "bi-diamond-fill"),
            >= 1900 => ("Diamond II", "#b9f2ff", "bi-diamond-fill"),
            >= 1800 => ("Diamond III", "#b9f2ff", "bi-diamond-fill"),
            >= 1700 => ("Emerald I", "#50c878", "bi-hexagon-fill"),
            >= 1600 => ("Emerald II", "#50c878", "bi-hexagon-fill"),
            >= 1500 => ("Emerald III", "#50c878", "bi-hexagon-fill"),
            >= 1400 => ("Platinum I", "#7dd8c0", "bi-shield-fill"),
            >= 1300 => ("Platinum II", "#7dd8c0", "bi-shield-fill"),
            >= 1200 => ("Platinum III", "#7dd8c0", "bi-shield-fill"),
            >= 1100 => ("Gold I", "#ffd700", "bi-trophy-fill"),
            >= 1000 => ("Gold II", "#ffd700", "bi-trophy-fill"),
            >= 900  => ("Gold III", "#ffd700", "bi-trophy-fill"),
            >= 800  => ("Silver I", "#c0c0c0", "bi-shield-shaded"),
            >= 700  => ("Silver II", "#c0c0c0", "bi-shield-shaded"),
            >= 600  => ("Silver III", "#c0c0c0", "bi-shield-shaded"),
            >= 500  => ("Bronze I", "#cd7f32", "bi-shield"),
            >= 400  => ("Bronze II", "#cd7f32", "bi-shield"),
            >= 300  => ("Bronze III", "#cd7f32", "bi-shield"),
            >= 200  => ("Iron I", "#71797e", "bi-shield-minus"),
            >= 100  => ("Iron II", "#71797e", "bi-shield-minus"),
            _       => ("Iron III", "#71797e", "bi-shield-minus"),
        };
    }

    /// <summary>Short tier name without subdivision (e.g. "Gold", "Challenger").</summary>
    public static string GetTierName(int rating)
    {
        return rating switch
        {
            >= 2700 => "Challenger",
            >= 2400 => "Grandmaster",
            >= 2100 => "Master",
            >= 1800 => "Diamond",
            >= 1500 => "Emerald",
            >= 1200 => "Platinum",
            >= 900  => "Gold",
            >= 600  => "Silver",
            >= 300  => "Bronze",
            _       => "Iron",
        };
    }

    /// <summary>
    /// CSS modifier class carrying the tier's colour as the <c>--rank-c</c> custom
    /// property (see css/9-overrides/dynamic-style-classes.css). One class per tier,
    /// matching the colours <see cref="GetRank"/> returns.
    /// </summary>
    public static string GetRankColorClass(int rating) => "rank-c-" + GetTierName(rating).ToLowerInvariant();
}
