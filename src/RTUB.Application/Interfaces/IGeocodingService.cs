namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for converting city names to geographic coordinates
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Get coordinates (latitude, longitude) for a city name asynchronously
    /// </summary>
    /// <param name="cityName">Name of the city</param>
    /// <param name="countryCode">Optional country code (e.g., "PT" for Portugal)</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Tuple with latitude and longitude, or null if not found</returns>
    Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string cityName, string? countryCode = "PT", CancellationToken cancellationToken = default);
}
