using System.Collections.Concurrent;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// In-memory queue for managing cities that need to be geocoded
/// Thread-safe implementation using ConcurrentQueue
/// </summary>
public class InMemoryGeocodingQueue : IGeocodingQueue
{
    private readonly ConcurrentQueue<(string CityName, string CountryCode)> _queue = new();
    private readonly ConcurrentDictionary<string, bool> _queuedCities = new();

    public Task EnqueueCityAsync(string cityName, string countryCode = "PT")
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return Task.CompletedTask;
        }

        var normalizedCity = cityName.Trim().ToLowerInvariant();
        var normalizedCountryCode = countryCode?.ToUpperInvariant() ?? "PT";
        var key = $"{normalizedCity}:{normalizedCountryCode}";

        // Only add if not already queued (avoid duplicates)
        if (_queuedCities.TryAdd(key, true))
        {
            _queue.Enqueue((normalizedCity, normalizedCountryCode));
        }

        return Task.CompletedTask;
    }

    public Task<List<(string CityName, string CountryCode)>> GetPendingCitiesAsync(int maxCount = 10)
    {
        var cities = new List<(string CityName, string CountryCode)>();

        for (int i = 0; i < maxCount && _queue.TryDequeue(out var city); i++)
        {
            cities.Add(city);
            // Remove from queued set
            var key = $"{city.CityName}:{city.CountryCode}";
            _queuedCities.TryRemove(key, out _);
        }

        return Task.FromResult(cities);
    }
}
