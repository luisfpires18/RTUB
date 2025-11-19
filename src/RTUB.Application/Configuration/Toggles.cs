namespace RTUB.Application.Configuration;

/// <summary>
/// General Toggles configuration
/// </summary>
public class Toggles
{
    public const string SectionName = "Toggles";
    
    /// <summary>
    /// Determines whether the member map feature is enabled
    /// </summary>
    public bool MapEnabled { get; set; } = true;
}
