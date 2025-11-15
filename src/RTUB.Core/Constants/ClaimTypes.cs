namespace RTUB.Core.Constants;

/// <summary>
/// Defines standard claim types used throughout the application
/// </summary>
public static class RtubClaimTypes
{
    /// <summary>
    /// Member category claim (LEITAO, CALOIRO, TUNO, VETERANO, TUNOSSAURO, etc.)
    /// Can have multiple values for users with multiple categories
    /// </summary>
    public const string Category = "rtub:category";
    
    /// <summary>
    /// Organizational position claim (MAGISTER, TESOUREIRO, etc.)
    /// Can have multiple values for users with multiple positions
    /// </summary>
    public const string Position = "rtub:position";
    
    /// <summary>
    /// Primary category - the highest/most relevant category for the user
    /// Used for display and simple authorization decisions
    /// </summary>
    public const string PrimaryCategory = "rtub:primary_category";
    
    /// <summary>
    /// Years as Tuno - numeric value
    /// </summary>
    public const string YearsAsTuno = "rtub:years_as_tuno";
    
    /// <summary>
    /// Whether user is a founder (1991)
    /// </summary>
    public const string IsFounder = "rtub:is_founder";
}
