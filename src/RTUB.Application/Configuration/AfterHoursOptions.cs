namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for RTUB After Hours. See docs/after_hours/README.md.
/// </summary>
public class AfterHoursOptions
{
    /// <summary>
    /// The configuration section name
    /// </summary>
    public const string SectionName = "AfterHours";

    /// <summary>
    /// Whether After Hours is reachable. Disabled by default: a missing section or key
    /// keeps the game closed, so an environment only opens it by setting it explicitly.
    /// </summary>
    public bool Enabled { get; set; }
}
