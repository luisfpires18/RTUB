using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services.Geocoding;

/// <summary>
/// Geocoding service using Nominatim (OpenStreetMap) API
/// Free service, no API key required
/// Uses database cache to avoid repeated API calls
/// </summary>
public partial class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingService> _logger;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
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
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("Nominatim");
        _httpClient.BaseAddress = new Uri(NominatimBaseUrl);
        // Nominatim requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RTUB-MemberMap/1.0");
        _logger = logger;
        _contextFactory = contextFactory;
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

        // Check database cache first (one context per operation, disposed here; the tests hand out a fresh context per call)
        await using var cacheCtx = _contextFactory.CreateDbContext();
        var cached = await cacheCtx.GeocodingCaches
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
            await using var context = _contextFactory.CreateDbContext();
            // Check if already exists (race condition protection)
            var existing = await context.GeocodingCaches
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

                context.GeocodingCaches.Add(cache);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Don't fail if caching fails - just log it
            _logger.LogError(ex, "Failed to cache coordinates for '{CityName}'", normalizedCity);
        }
    }

}
