namespace RTUB.Application.Services.Retirement;

/// <summary>
/// Result of retirement status evaluation
/// Contains calculated retirement status and activity metrics
/// </summary>
public class RetirementStatusResult
{
    /// <summary>
    /// Whether the user should be marked as retired
    /// </summary>
    public bool IsRetired { get; set; }

    /// <summary>
    /// Date of first activity (rehearsal attendance or event enrollment)
    /// </summary>
    public DateTime? FirstActivityDate { get; set; }

    /// <summary>
    /// Date of most recent activity (rehearsal attendance or event enrollment)
    /// </summary>
    public DateTime? LastActivityDate { get; set; }

    /// <summary>
    /// Number of months since last activity
    /// </summary>
    public int MonthsSinceLastActivity { get; set; }

    /// <summary>
    /// Whether user has minimum activity history (at least 1 activity)
    /// </summary>
    public bool HasMinimumHistory { get; set; }
}
