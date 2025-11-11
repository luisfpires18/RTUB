using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace RTUB.Controllers;

/// <summary>
/// Controller for serving static images with E-Tag caching support.
/// This handles images stored locally in wwwroot/images that are not on Cloudflare R2.
/// </summary>
[ApiController]
[Route("[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(IWebHostEnvironment environment, ILogger<ImagesController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Serves an image file with E-Tag caching support.
    /// Returns 304 Not Modified if the E-Tag matches.
    /// </summary>
    /// <param name="filename">The image filename (e.g., "default-avatar.webp")</param>
    [HttpGet("{filename}")]
    [ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Any)] // 30 days
    public IActionResult GetImage(string filename)
    {
        try
        {
            // Security: Only allow specific safe filenames to prevent directory traversal
            if (string.IsNullOrWhiteSpace(filename) || 
                filename.Contains("..") || 
                filename.Contains("/") || 
                filename.Contains("\\"))
            {
                return BadRequest("Invalid filename");
            }

            // Get the file path
            var imagePath = Path.Combine(_environment.WebRootPath, "images", filename);

            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound();
            }

            // Get file info for E-Tag generation
            var fileInfo = new FileInfo(imagePath);
            var lastModified = fileInfo.LastWriteTimeUtc;

            // Generate E-Tag based on last modified time and file size
            var etag = new EntityTagHeaderValue($"\"{lastModified.ToFileTime():x}-{fileInfo.Length:x}\"");

            // Set LastModified header
            Response.GetTypedHeaders().LastModified = lastModified;

            // Check If-None-Match (E-Tag)
            if (Request.Headers.IfNoneMatch.Any())
            {
                var requestEtags = Request.GetTypedHeaders().IfNoneMatch;
                if (requestEtags != null)
                {
                    foreach (var requestEtag in requestEtags)
                    {
                        if (etag.Compare(requestEtag, useStrongComparison: true))
                        {
                            return StatusCode(StatusCodes.Status304NotModified);
                        }
                    }
                }
            }

            // Check If-Modified-Since
            if (Request.Headers.IfModifiedSince.Any())
            {
                var ifModifiedSince = Request.GetTypedHeaders().IfModifiedSince;
                if (ifModifiedSince.HasValue && lastModified <= ifModifiedSince.Value)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Determine content type based on file extension
            var contentType = filename.ToLowerInvariant() switch
            {
                var f when f.EndsWith(".webp") => "image/webp",
                var f when f.EndsWith(".png") => "image/png",
                var f when f.EndsWith(".jpg") || f.EndsWith(".jpeg") => "image/jpeg",
                var f when f.EndsWith(".svg") => "image/svg+xml",
                var f when f.EndsWith(".gif") => "image/gif",
                _ => "application/octet-stream"
            };

            // Return the file with E-Tag header
            var fileBytes = System.IO.File.ReadAllBytes(imagePath);
            Response.GetTypedHeaders().ETag = etag;
            
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            // Sanitize filename for logging to prevent log forging
            var sanitizedFilename = filename?.Replace("\r", "").Replace("\n", "") ?? "unknown";
            _logger.LogError(ex, "Error serving image {Filename}", sanitizedFilename);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
