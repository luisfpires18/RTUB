# Geocoding Service Documentation

The RTUB Member Map feature uses geocoding to convert city names to GPS coordinates. By default, it uses **Nominatim** (OpenStreetMap's free geocoding service), but you can optionally configure it to use **Google Maps Geocoding API** if you prefer.

## Current Implementation: Nominatim (Free, No API Key Required)

### About Nominatim
- **Free**: No API key required
- **Open Source**: Powered by OpenStreetMap
- **Usage Policy**: Reasonable fair use (max 1 request/second)
- **Coverage**: Global, including all Portuguese cities, towns, and villages
- **Accuracy**: High accuracy for cities, towns, villages, and even small hamlets

### Advanced Features
- **Multi-strategy search**: Tries multiple search approaches to find the best match
  - First tries as city/town/village
  - Then searches for smaller settlements (hamlets, suburbs)
  - Falls back to broader search if needed
- **Automatic caching** to reduce API calls and improve performance
- **Case-insensitive matching**
- **Accent-aware** (works with "Bragança", "Évora", etc.)
- **Country code filtering** (defaults to "PT" for Portugal)
- **Small village support**: Now finds even small villages like Mogadouro, Vinhais, etc.
- **Smart matching**: Prioritizes exact name matches and settlement types

### How It Works
When you enter a city name like "Bragança" or "Mogadouro", the service:
1. Checks the cache first (instant response if already geocoded)
2. Tries multiple search strategies with Nominatim API:
   - Searches for cities, towns, and villages with country filter
   - Expands to include hamlets and smaller settlements
   - Uses full country name in query for better matching
   - Falls back to broader search if needed
3. Selects the best match based on:
   - Exact name match
   - Settlement type priority (city > town > village > hamlet)
   - Nominatim's importance score
4. Caches the result for future use

### No Configuration Needed
The default Nominatim service works out of the box with no setup required!

---

## Alternative: Google Maps Geocoding API (Optional)

If you need higher rate limits or prefer Google Maps, you can switch to Google's Geocoding API.

### Step 1: Create a Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click **Create Project**
3. Enter a project name (e.g., "RTUB Member Map")
4. Click **Create**

### Step 2: Enable the Geocoding API

1. In the Cloud Console, go to **APIs & Services** > **Library**
2. Search for "Geocoding API"
3. Click on **Geocoding API**
4. Click **Enable**

### Step 3: Create an API Key

1. Go to **APIs & Services** > **Credentials**
2. Click **Create Credentials** > **API Key**
3. Your API key will be created and displayed
4. **IMPORTANT**: Click **Restrict Key** to secure it

### Step 4: Restrict Your API Key (Recommended)

To prevent unauthorized use:

1. Under **API restrictions**:
   - Select **Restrict key**
   - Check **Geocoding API**
   
2. Under **Application restrictions**:
   - Choose **HTTP referrers (web sites)**
   - Add your website URLs:
     ```
     https://yourdomain.com/*
     https://www.yourdomain.com/*
     ```
   - For local development, add:
     ```
     http://localhost:*
     https://localhost:*
     ```

3. Click **Save**

### Step 5: Add API Key to Configuration

Add to your `appsettings.json` or `appsettings.Production.json`:

```json
{
  "Geocoding": {
    "Provider": "GoogleMaps",
    "GoogleMapsApiKey": "YOUR_API_KEY_HERE"
  }
}
```

**For production**, use environment variables instead:
```bash
export Geocoding__Provider=GoogleMaps
export Geocoding__GoogleMapsApiKey=YOUR_API_KEY_HERE
```

### Step 6: Implement Google Maps Geocoding Service

Create a new file `src/RTUB.Application/Services/GoogleMapsGeocodingService.cs`:

```csharp
using RTUB.Application.Interfaces;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services;

public class GoogleMapsGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleMapsGeocodingService> _logger;
    private readonly string _apiKey;
    private readonly Dictionary<string, (double Latitude, double Longitude)?> _cache = new();

    public GoogleMapsGeocodingService(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration,
        ILogger<GoogleMapsGeocodingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GoogleMaps");
        _apiKey = configuration["Geocoding:GoogleMapsApiKey"] 
            ?? throw new InvalidOperationException("Google Maps API key not configured");
        _logger = logger;
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(
        string cityName, 
        string? countryCode = "PT")
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return null;

        var cacheKey = $"{cityName.ToLowerInvariant()}_{countryCode}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var address = $"{cityName}, {countryCode}";
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_apiKey}";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GoogleMapsResponse>(content);
            
            if (result?.results?.Length > 0)
            {
                var location = result.results[0].geometry.location;
                var coordinates = (location.lat, location.lng);
                _cache[cacheKey] = coordinates;
                return coordinates;
            }
            
            _cache[cacheKey] = null;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error geocoding {City}", cityName);
            return null;
        }
    }

    private class GoogleMapsResponse
    {
        public Result[]? results { get; set; }
        
        public class Result
        {
            public Geometry geometry { get; set; } = new();
        }
        
        public class Geometry
        {
            public Location location { get; set; } = new();
        }
        
        public class Location
        {
            public double lat { get; set; }
            public double lng { get; set; }
        }
    }
}
```

### Step 7: Update Program.cs

Modify `src/RTUB.Web/Program.cs` to conditionally use Google Maps:

```csharp
// --------- Geocoding Service ---------
var geocodingProvider = builder.Configuration["Geocoding:Provider"] ?? "Nominatim";

if (geocodingProvider == "GoogleMaps")
{
    services.AddHttpClient("GoogleMaps")
        .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));
    services.AddSingleton<IGeocodingService, GoogleMapsGeocodingService>();
}
else
{
    services.AddHttpClient("Nominatim")
        .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));
    services.AddSingleton<IGeocodingService, NominatimGeocodingService>();
}
```

### Pricing

**Google Maps Geocoding API Pricing** (as of 2024):
- **Free tier**: $200 credit per month (covers ~40,000 requests)
- **After free tier**: $5.00 per 1,000 requests
- First-time users get $300 credit for 90 days

For most applications, the free tier is sufficient.

### Rate Limits

- **Nominatim**: ~1 request/second (fair use)
- **Google Maps**: 50 requests/second (can request increases)

---

## Recommendation

**Start with Nominatim** (the default):
- ✅ Free forever
- ✅ No setup required
- ✅ No API key management
- ✅ Sufficient for most use cases
- ✅ Automatic caching reduces API calls

**Switch to Google Maps only if**:
- You need higher rate limits
- You're already using other Google Maps services
- You need specific features not available in Nominatim

---

## Testing Your Setup

After configuration, test your geocoding service:

```bash
# Navigate to the test project
cd tests/RTUB.Application.Tests

# Run geocoding tests
dotnet test --filter "NominatimGeocodingService"

# Or for Google Maps (after implementing)
dotnet test --filter "GoogleMapsGeocodingService"
```

## Troubleshooting

### Nominatim Issues

**Problem**: "Too Many Requests" error
- **Solution**: Reduce frequency of API calls, caching should help

**Problem**: City not found
- **Solution**: Try different name variations or add country context

### Google Maps Issues

**Problem**: "API key not valid"
- **Solution**: Check API key restrictions and ensure Geocoding API is enabled

**Problem**: "REQUEST_DENIED"
- **Solution**: Enable billing on your Google Cloud project

**Problem**: High costs
- **Solution**: Implement caching (already included in the code)

---

## Security Best Practices

1. **Never commit API keys** to version control
2. **Use environment variables** for production
3. **Restrict API keys** to specific domains/IPs
4. **Monitor usage** in Google Cloud Console
5. **Set up billing alerts** to avoid surprises

---

## Support

For issues or questions:
- Nominatim: [https://nominatim.org/](https://nominatim.org/)
- Google Maps: [https://developers.google.com/maps/documentation/geocoding](https://developers.google.com/maps/documentation/geocoding)
