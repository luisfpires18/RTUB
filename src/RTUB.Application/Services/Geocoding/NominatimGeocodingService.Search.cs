using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services.Geocoding;

// Nominatim geocoding: querying strategies and picking the best match.

public partial class NominatimGeocodingService
{
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
