namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for storage services
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// The configuration section name
    /// </summary>
    public const string SectionName = "Storage";

    /// <summary>
    /// URL expiration time in minutes for pre-signed URLs.
    /// Default is 60 minutes (1 hour).
    /// </summary>
    public int UrlExpirationMinutes { get; set; } = 60;
}
