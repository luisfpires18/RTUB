namespace RTUB.Core.Entities;

/// <summary>
/// Persistent cache for geocoded locations
/// Stores city/location coordinates to avoid repeated API calls
/// </summary>
public class GeocodingCache : BaseEntity
{
    /// <summary>
    /// City or location name (normalized, lowercased)
    /// </summary>
    public string CityName { get; set; } = "";

    /// <summary>
    /// Country code (e.g., "PT", "ES")
    /// </summary>
    public string CountryCode { get; set; } = "PT";

    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Last time this entry was updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source of the geocoding data (e.g., "Nominatim", "GoogleMaps")
    /// </summary>
    public string Source { get; set; } = "Nominatim";
}
