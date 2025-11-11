namespace RTUB.Core.Constants;

/// <summary>
/// Custom claim types for RTUB application
/// </summary>
public static class CustomClaimTypes
{
    /// <summary>
    /// Claim type for member categories (Tuno, Veterano, Tunossauro, etc.)
    /// Multiple claims of this type can exist for one user
    /// </summary>
    public const string Category = "rtub:category";
    
    /// <summary>
    /// Claim type for organizational positions (Magister, Secretario, etc.)
    /// Multiple claims of this type can exist for one user
    /// </summary>
    public const string Position = "rtub:position";
}
