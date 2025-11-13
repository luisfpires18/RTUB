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
    /// Supports subdirectories (e.g., "hierarchy/bag_leitao.webp")
    /// </summary>
    /// <param name="**">The image path including subdirectories and filename</param>
    [HttpGet("{**imagePath}")]
    public IActionResult GetImage(string imagePath)
    {
        try
        {
            // Security: Only allow specific safe paths to prevent directory traversal
            if (string.IsNullOrWhiteSpace(imagePath) || 
                imagePath.Contains("..") || 
                imagePath.Contains("\\"))
            {
                return BadRequest("Invalid image path");
            }

            // Normalize path separators
            var normalizedPath = imagePath.Replace("/", Path.DirectorySeparatorChar.ToString());
            
            // Get the file path
            var fullImagePath = Path.Combine(_environment.WebRootPath, "images", normalizedPath);

            if (!System.IO.File.Exists(fullImagePath))
            {
                return NotFound();
            }

            // Get file info for E-Tag generation
            var fileInfo = new FileInfo(fullImagePath);
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
            var fileName = Path.GetFileName(imagePath);
            var contentType = fileName.ToLowerInvariant() switch
            {
                var f when f.EndsWith(".webp") => "image/webp",
                var f when f.EndsWith(".png") => "image/png",
                var f when f.EndsWith(".jpg") || f.EndsWith(".jpeg") => "image/jpeg",
                var f when f.EndsWith(".svg") => "image/svg+xml",
                var f when f.EndsWith(".gif") => "image/gif",
                _ => "application/octet-stream"
            };

            // Set Cache-Control to allow browser caching but require revalidation with E-Tag
            // no-cache means "you must revalidate before using cached copy"
            Response.Headers.CacheControl = "no-cache";

            // Return the file with E-Tag header
            var fileBytes = System.IO.File.ReadAllBytes(fullImagePath);
            Response.GetTypedHeaders().ETag = etag;
            
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            // Sanitize filename for logging to prevent log forging
            var sanitizedPath = imagePath?.Replace("\r", "").Replace("\n", "") ?? "unknown";
            _logger.LogError(ex, "Error serving image {ImagePath}", sanitizedPath);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
