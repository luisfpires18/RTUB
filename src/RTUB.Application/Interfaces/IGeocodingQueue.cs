namespace RTUB.Application.Interfaces;

/// <summary>
/// Queue for managing cities that need to be geocoded in the background
/// </summary>
public interface IGeocodingQueue
{
    /// <summary>
    /// Enqueue a city for geocoding
    /// </summary>
    /// <param name="cityName">Name of the city</param>
    /// <param name="countryCode">Optional country code (e.g., "PT" for Portugal)</param>
    Task EnqueueCityAsync(string cityName, string countryCode = "PT");

    /// <summary>
    /// Get pending cities to be geocoded
    /// </summary>
    /// <param name="maxCount">Maximum number of cities to return</param>
    /// <returns>List of cities to geocode</returns>
    Task<List<(string CityName, string CountryCode)>> GetPendingCitiesAsync(int maxCount = 10);
}
