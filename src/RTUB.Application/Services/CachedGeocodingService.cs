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
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IGeocodingQueue _geocodingQueue;
    private readonly ILogger<CachedGeocodingService> _logger;

    public CachedGeocodingService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IGeocodingQueue geocodingQueue,
        ILogger<CachedGeocodingService> logger)
    {
        _contextFactory = contextFactory;
        _geocodingQueue = geocodingQueue;
        _logger = logger;
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string cityName, string? countryCode = "PT", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var normalizedCity = cityName.Trim().ToLowerInvariant();
        var normalizedCountryCode = countryCode?.ToUpperInvariant() ?? "PT";

        // Check database cache only (no HTTP calls)
        var ctx = _contextFactory.CreateDbContext();
        var cached = await ctx.GeocodingCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.CityName == normalizedCity && g.CountryCode == normalizedCountryCode, cancellationToken);

        if (cached != null)
        {
            return (cached.Latitude, cached.Longitude);
        }

        // Not in cache - enqueue for background geocoding
        await _geocodingQueue.EnqueueCityAsync(normalizedCity, normalizedCountryCode);

        return null;
    }
}
