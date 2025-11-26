using RTUB.Application.Interfaces;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RTUB.Application.Services.Geocoding;

/// <summary>
/// Geocoding service using Nominatim (OpenStreetMap) API
/// Free service, no API key required
/// Uses database cache to avoid repeated API calls
/// </summary>
public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly bool _disabledInTests;
    private const string NominatimBaseUrl = "https://nominatim.openstreetmap.org";

    // Rate limiting: Nominatim requires ~1 request per second
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private const int MinMillisecondsBetweenRequests = 1100; // Slightly over 1 second to be safe

    // Local fallback coordinates for specific cities (override remote geocoding)
    private static readonly Dictionary<string, (double Latitude, double Longitude)> LocalFallbackCoordinates = new(StringComparer.OrdinalIgnoreCase)
    {
        { "bragança", (41.80582, -6.75719) }
    };

    public NominatimGeocodingService(
        IHttpClientFactory httpClientFactory,
        ILogger<NominatimGeocodingService> logger,
        ApplicationDbContext dbContext,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("Nominatim");
        _httpClient.BaseAddress = new Uri(NominatimBaseUrl);
        // Nominatim requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RTUB-MemberMap/1.0");
        _logger = logger;
        _dbContext = dbContext;
        _disabledInTests = configuration.GetValue("Geocoding:DisabledInTests", false);
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string cityName, string? countryCode = "PT", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var normalizedCity = cityName.Trim().ToLowerInvariant();
        var normalizedCountryCode = countryCode?.ToUpperInvariant() ?? "PT";

        // If disabled in tests, return dummy coordinates
        if (_disabledInTests)
        {
            return (38.7223, -9.1393); // Default to Lisbon coordinates
        }

        // Check database cache first
        var cached = await _dbContext.GeocodingCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.CityName == normalizedCity && g.CountryCode == normalizedCountryCode, cancellationToken);

        if (cached != null)
        {
            return (cached.Latitude, cached.Longitude);
        }

        // Not in cache - geocode and store
        try
        {
            // Try geocoding with strategies
            var coordinates = await TryGeocodeWithStrategies(cityName, normalizedCountryCode, cancellationToken);

            if (coordinates.HasValue)
            {
                // Store in database cache
                await StoreInCache(normalizedCity, normalizedCountryCode, coordinates.Value);
            }
            else
            {
                _logger.LogWarning("No geocoding results found for '{CityName}'", cityName);
            }

            return coordinates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error geocoding '{CityName}'", cityName);
            return null;
        }
    }

    private async Task StoreInCache(string normalizedCity, string countryCode, (double Latitude, double Longitude) coordinates)
    {
        try
        {
            // Check if already exists (race condition protection)
            var existing = await _dbContext.GeocodingCaches
                .FirstOrDefaultAsync(g => g.CityName == normalizedCity && g.CountryCode == countryCode);

            if (existing == null)
            {
                var cache = new GeocodingCache
                {
                    CityName = normalizedCity,
                    CountryCode = countryCode,
                    Latitude = coordinates.Latitude,
                    Longitude = coordinates.Longitude,
                    LastUpdated = DateTime.UtcNow,
                    Source = "Nominatim"
                };

                _dbContext.GeocodingCaches.Add(cache);
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Don't fail if caching fails - just log it
            _logger.LogError(ex, "Failed to cache coordinates for '{CityName}'", normalizedCity);
        }
    }

    private async Task<(double Latitude, double Longitude)?> TryGeocodeWithStrategies(string cityName, string? countryCode, CancellationToken cancellationToken)
    {
        // Check local fallback coordinates first (explicit overrides)
        if (LocalFallbackCoordinates.TryGetValue(cityName, out var fallbackCoords))
        {
            _logger.LogInformation("Using local fallback coordinates for '{CityName}'", cityName);
            return fallbackCoords;
        }

        // Strategy 1: Search as city with country filter (most common case)
        var result = await TryGeocode(cityName, countryCode, "city,town,village,municipality", cancellationToken);
        if (result.HasValue) return result;

        // Strategy 2: Include smaller settlements (for villages)
        result = await TryGeocode(cityName, countryCode, "city,town,village,hamlet,municipality,suburb", cancellationToken);
        if (result.HasValue) return result;

        // Strategy 3: Broader search without strict type filtering (last resort)
        result = await TryGeocode(cityName, countryCode, null, cancellationToken);
        return result;
    }

    private async Task<(double Latitude, double Longitude)?> TryGeocode(string query, string? countryCode, string? featureType, CancellationToken cancellationToken)
    {
        // Rate limiting: ensure we don't exceed 1 request per second
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest.TotalMilliseconds < MinMillisecondsBetweenRequests)
            {
                var delayMs = MinMillisecondsBetweenRequests - (int)timeSinceLastRequest.TotalMilliseconds;
                await Task.Delay(delayMs, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }

        var encodedQuery = Uri.EscapeDataString(query);
        var urlBuilder = new StringBuilder($"/search?q={encodedQuery}&format=json&limit=3&addressdetails=1");

        if (!string.IsNullOrEmpty(countryCode))
        {
            urlBuilder.Append($"&countrycodes={countryCode.ToLowerInvariant()}");
        }

        var url = urlBuilder.ToString();

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<NominatimResult>>(content);

            if (results == null || results.Count == 0)
            {
                return null;
            }

            // Find the best match
            var bestResult = FindBestMatch(results, query, featureType);

            if (bestResult != null)
            {
                var coordinates = ExtractCoordinates(bestResult);
                return coordinates;
            }

            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Nominatim request timed out for query: {Query}", query);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Nominatim API for query: {Query}", query);
            return null;
        }
    }

    private (double Latitude, double Longitude) ExtractCoordinates(NominatimResult result)
    {
        // If result has a bounding box and is a place (city/town/village), compute center
        if (result.boundingbox != null &&
            result.boundingbox.Count == 4 &&
            result.@class == "place" &&
            result.type != null &&
            new[] { "city", "town", "village" }.Contains(result.type.ToLowerInvariant()))
        {
            try
            {
                // Bounding box format: [south, north, west, east]
                var south = double.Parse(result.boundingbox[0], System.Globalization.CultureInfo.InvariantCulture);
                var north = double.Parse(result.boundingbox[1], System.Globalization.CultureInfo.InvariantCulture);
                var west = double.Parse(result.boundingbox[2], System.Globalization.CultureInfo.InvariantCulture);
                var east = double.Parse(result.boundingbox[3], System.Globalization.CultureInfo.InvariantCulture);

                var centerLat = (south + north) / 2.0;
                var centerLon = (west + east) / 2.0;

                _logger.LogDebug("Using bounding box center for {DisplayName}: ({Lat}, {Lon})",
                    result.display_name, centerLat, centerLon);

                return (centerLat, centerLon);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse bounding box for {DisplayName}, falling back to direct coordinates",
                    result.display_name);
            }
        }

        // Fallback to direct lat/lon
        var latitude = double.Parse(result.lat, System.Globalization.CultureInfo.InvariantCulture);
        var longitude = double.Parse(result.lon, System.Globalization.CultureInfo.InvariantCulture);

        return (latitude, longitude);
    }

    private NominatimResult? FindBestMatch(List<NominatimResult> results, string query, string? featureType)
    {
        if (results.Count == 0) return null;

        var queryLower = query.ToLowerInvariant();
        var preferredTypes = new[] { "city", "town", "village", "hamlet", "municipality" };

        // First, try to find exact name match with class="place" and preferred type
        foreach (var result in results)
        {
            var nameMatch = result.display_name.Split(',')[0].Trim().ToLowerInvariant();
            if (nameMatch == queryLower &&
                result.@class == "place" &&
                result.type != null &&
                preferredTypes.Contains(result.type.ToLowerInvariant()))
            {
                return result;
            }
        }

        // Second, find any result with class="place" and preferred type
        foreach (var result in results)
        {
            if (result.@class == "place" &&
                result.type != null &&
                preferredTypes.Contains(result.type.ToLowerInvariant()))
            {
                return result;
            }
        }

        // Third, find any result with preferred type (even without class="place")
        foreach (var result in results)
        {
            if (result.type != null && preferredTypes.Contains(result.type.ToLowerInvariant()))
            {
                return result;
            }
        }

        // Finally, return the first result with highest importance
        return results.OrderByDescending(r => r.importance).FirstOrDefault();
    }

}
