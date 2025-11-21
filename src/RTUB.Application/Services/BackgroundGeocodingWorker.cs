using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Geocoding;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that processes the geocoding queue
/// Runs every 30 seconds and geocodes pending cities
/// </summary>
public class BackgroundGeocodingWorker : BackgroundService
{
    private readonly ILogger<BackgroundGeocodingWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IGeocodingQueue _geocodingQueue;
    private const int ProcessingIntervalSeconds = 30;

    public BackgroundGeocodingWorker(
        ILogger<BackgroundGeocodingWorker> logger,
        IServiceScopeFactory serviceScopeFactory,
        IGeocodingQueue geocodingQueue)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _geocodingQueue = geocodingQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit before starting the first processing cycle to allow the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing geocoding queue");
            }

            // Wait before next processing cycle
            await Task.Delay(TimeSpan.FromSeconds(ProcessingIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        // Get pending cities from queue
        var cities = await _geocodingQueue.GetPendingCitiesAsync(maxCount: 10);

        if (cities.Count == 0)
        {
            return;
        }

        // Create a scope for scoped services (like DbContext)
        using var scope = _serviceScopeFactory.CreateScope();
        var geocodingService = scope.ServiceProvider.GetRequiredService<NominatimGeocodingService>();

        int successCount = 0;
        int failureCount = 0;

        foreach (var (cityName, countryCode) in cities)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var coordinates = await geocodingService.GetCoordinatesAsync(cityName, countryCode);

                if (coordinates.HasValue)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                    _logger.LogWarning("Failed to geocode {CityName}", cityName);
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex, "Error geocoding {CityName}", cityName);
            }
        }
    }
}
