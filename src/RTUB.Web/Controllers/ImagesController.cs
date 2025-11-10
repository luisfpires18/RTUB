using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RTUB.Web.Controllers;

/// <summary>
/// Controller for serving images from Cloudflare R2 with proper ETag caching support
/// Proxies images through the application to enable 304 Not Modified responses
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<ImagesController> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger;
        
        var bucketName = configuration["Cloudflare:R2:Bucket"];
        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = "Cloudflare R2 bucket name not configured. Set Cloudflare:R2:Bucket.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
        
        _bucketName = bucketName;
    }

    /// <summary>
    /// Serves an image from S3/R2 with proper ETag caching support
    /// Handles If-None-Match header to return 304 Not Modified when content hasn't changed
    /// </summary>
    /// <param name="path">The full object key path (e.g., images/Production/profile/123_timestamp.webp)</param>
    /// <returns>Image file with proper caching headers</returns>
    [HttpGet("{**path}")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any, VaryByHeader = "If-None-Match")]
    public async Task<IActionResult> GetImage(string path)
    {
        try
        {
            // Normalize the path (remove leading slash if present)
            var objectKey = path.TrimStart('/');
            
            // Get object metadata first to check ETag
            var metadataRequest = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };
            
            GetObjectMetadataResponse metadata;
            try
            {
                metadata = await _s3Client.GetObjectMetadataAsync(metadataRequest);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Image not found: {ObjectKey}", objectKey);
                return NotFound();
            }
            
            // Check If-None-Match header (contains ETags from browser)
            var clientETag = Request.Headers["If-None-Match"].ToString();
            var serverETag = metadata.ETag;
            
            // If ETags match, return 304 Not Modified
            if (!string.IsNullOrEmpty(clientETag) && clientETag == serverETag)
            {
                _logger.LogDebug("ETag match for {ObjectKey}, returning 304", objectKey);
                return StatusCode(304); // Not Modified
            }
            
            // ETags don't match or no client ETag, fetch the full object
            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };
            
            using var response = await _s3Client.GetObjectAsync(getRequest);
            
            // Read the image data
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            var imageData = memoryStream.ToArray();
            
            // Set caching headers
            Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
            Response.Headers.Append("ETag", serverETag);
            
            // Since timestamp is in URL, content is immutable
            // Set Last-Modified for additional caching support
            if (response.LastModified.HasValue)
            {
                Response.Headers.Append("Last-Modified", response.LastModified.Value.ToUniversalTime().ToString("R"));
            }
            
            _logger.LogDebug("Serving image {ObjectKey} with ETag {ETag}", objectKey, serverETag);
            
            // Return the image with proper content type
            return File(imageData, response.Headers.ContentType ?? "image/webp");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error fetching image {Path}. ErrorCode: {ErrorCode}", path, ex.ErrorCode);
            return StatusCode(500, "Error fetching image");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching image {Path}", path);
            return StatusCode(500, "Error fetching image");
        }
    }
}
