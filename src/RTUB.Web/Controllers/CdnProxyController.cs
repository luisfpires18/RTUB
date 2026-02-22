using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace RTUB.Controllers;

/// <summary>
/// Lightweight proxy for Cloudflare R2 images to avoid CORS issues when loaded by PixiJS.
/// Caches responses in-memory to minimize upstream requests.
/// </summary>
[ApiController]
[Route("api/cdn")]
public class CdnProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CdnProxyController> _logger;

    public CdnProxyController(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<CdnProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Proxies an image from the Cloudflare R2 public bucket.
    /// Only allows paths under the configured R2 public URL.
    /// GET /api/cdn/image?path=images/production/drinks/fino.webp
    /// </summary>
    [HttpGet("image")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)] // 24h browser cache
    public async Task<IActionResult> GetImage([FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest("Missing path parameter.");

        // Security: block directory traversal
        if (path.Contains("..") || path.Contains('\\'))
            return BadRequest("Invalid path.");

        // Only allow image extensions
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".webp" or ".png" or ".jpg" or ".jpeg" or ".svg" or ".gif"))
            return BadRequest("Unsupported file type.");

        var publicUrl = _configuration["Cloudflare:R2:PublicUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(publicUrl))
            return StatusCode(503, "CDN not configured.");

        var fullUrl = $"{publicUrl}/{path.TrimStart('/')}";

        // Check in-memory cache first
        var cacheKey = $"cdn_proxy_{path}";
        if (_cache.TryGetValue(cacheKey, out CachedImage? cached) && cached != null)
        {
            return File(cached.Data, cached.ContentType);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("CdnProxy");
            using var response = await client.GetAsync(fullUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CDN proxy: upstream returned {StatusCode} for {Path}", response.StatusCode, path);
                return StatusCode((int)response.StatusCode);
            }

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            // Cache for 1 hour in memory (images rarely change)
            _cache.Set(cacheKey, new CachedImage(data, contentType), TimeSpan.FromHours(1));

            return File(data, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CDN proxy error for path {Path}", path);
            return StatusCode(502, "Failed to fetch from CDN.");
        }
    }

    private sealed record CachedImage(byte[] Data, string ContentType);
}
