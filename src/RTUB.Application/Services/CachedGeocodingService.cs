using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Geocoding service that only reads from cache (no HTTP calls)
/// If coordinates not found, enqueues the city for background geocoding
/// This service is used by the UI for fast, non-blocking geocoding
/// </summary>
public class CachedGeocodingService : IGeocodingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGeocodingQueue _geocodingQueue;
    private readonly ILogger<CachedGeocodingService> _logger;

    public CachedGeocodingService(
        ApplicationDbContext dbContext,
        IGeocodingQueue geocodingQueue,
        ILogger<CachedGeocodingService> logger)
    {
        _dbContext = dbContext;
        _geocodingQueue = geocodingQueue;
        _logger = logger;
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string cityName, string? countryCode = "PT")
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var normalizedCity = cityName.Trim().ToLowerInvariant();
        var normalizedCountryCode = countryCode?.ToUpperInvariant() ?? "PT";

        // Check database cache only (no HTTP calls)
        var cached = await _dbContext.GeocodingCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.CityName == normalizedCity && g.CountryCode == normalizedCountryCode);

        if (cached != null)
        {
            _logger.LogDebug("Returning cached coordinates for '{CityName}'", cityName);
            return (cached.Latitude, cached.Longitude);
        }

        // Not in cache - enqueue for background geocoding
        _logger.LogDebug("City '{CityName}' not in cache, enqueueing for background geocoding", cityName);
        await _geocodingQueue.EnqueueCityAsync(normalizedCity, normalizedCountryCode);

        return null;
    }
}
