using RTUB.Application.Interfaces;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RTUB.Application.Services;

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

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string cityName, string? countryCode = "PT")
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
            _logger.LogDebug("Geocoding disabled in tests, returning dummy coordinates for '{CityName}'", cityName);
            return (38.7223, -9.1393); // Default to Lisbon coordinates
        }
        
        // Check database cache first
        var cached = await _dbContext.GeocodingCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.CityName == normalizedCity && g.CountryCode == normalizedCountryCode);
        
        if (cached != null)
        {
            _logger.LogDebug("Returning cached coordinates for '{CityName}' from database", cityName);
            return (cached.Latitude, cached.Longitude);
        }
        
        // Not in cache - geocode and store
        try
        {
            // Try geocoding with strategies
            var coordinates = await TryGeocodeWithStrategies(cityName, normalizedCountryCode);
            
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

    private async Task<(double Latitude, double Longitude)?> TryGeocodeWithStrategies(string cityName, string? countryCode)
    {
        // Strategy 1: Search as city with country filter (most common case)
        var result = await TryGeocode(cityName, countryCode, "city,town,village,municipality");
        if (result.HasValue) return result;

        // Strategy 2: Include smaller settlements (for villages)
        result = await TryGeocode(cityName, countryCode, "city,town,village,hamlet,municipality,suburb");
        if (result.HasValue) return result;

        // Strategy 3: Broader search without strict type filtering (last resort)
        result = await TryGeocode(cityName, countryCode, null);
        return result;
    }

    private async Task<(double Latitude, double Longitude)?> TryGeocode(string query, string? countryCode, string? featureType)
    {
        // Rate limiting: ensure we don't exceed 1 request per second
        await _rateLimiter.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest.TotalMilliseconds < MinMillisecondsBetweenRequests)
            {
                var delayMs = MinMillisecondsBetweenRequests - (int)timeSinceLastRequest.TotalMilliseconds;
                _logger.LogDebug("Rate limiting: waiting {DelayMs}ms", delayMs);
                await Task.Delay(delayMs);
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
        
        _logger.LogDebug("Nominatim API query: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url);
            
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
                var coordinates = (
                    Latitude: double.Parse(bestResult.lat, System.Globalization.CultureInfo.InvariantCulture),
                    Longitude: double.Parse(bestResult.lon, System.Globalization.CultureInfo.InvariantCulture)
                );
                
                _logger.LogDebug("Match found: {DisplayName}", bestResult.display_name);
                
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

    private NominatimResult? FindBestMatch(List<NominatimResult> results, string query, string? featureType)
    {
        if (results.Count == 0) return null;
        
        var queryLower = query.ToLowerInvariant();
        var preferredTypes = new[] { "city", "town", "village", "hamlet", "municipality" };

        // First, try to find exact name match with preferred type
        foreach (var result in results)
        {
            var nameMatch = result.display_name.Split(',')[0].Trim().ToLowerInvariant();
            if (nameMatch == queryLower && 
                result.type != null && 
                preferredTypes.Contains(result.type.ToLowerInvariant()))
            {
                return result;
            }
        }

        // Second, find any result with preferred type
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

    private class NominatimResult
    {
        public string lat { get; set; } = "";
        public string lon { get; set; } = "";
        public string display_name { get; set; } = "";
        public string? type { get; set; }
        public double importance { get; set; }
        public Address? address { get; set; }
    }

    private class Address
    {
        public string? city { get; set; }
        public string? town { get; set; }
        public string? village { get; set; }
        public string? hamlet { get; set; }
        public string? municipality { get; set; }
        public string? country { get; set; }
    }
}
