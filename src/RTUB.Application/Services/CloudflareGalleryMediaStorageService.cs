using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Cloudflare R2 storage service for gallery media
/// </summary>
public class CloudflareGalleryMediaStorageService : BaseCloudflareStorageService<CloudflareGalleryMediaStorageService>, IGalleryMediaStorageService
{
    private readonly IConfiguration _configuration;
    private readonly string _publicUrl;

    // Max file sizes
    private const long MaxImageSize = 10 * 1024 * 1024; // 10MB for images
    private const long MaxVideoSizeDefault = 100 * 1024 * 1024; // 100MB default for videos

    public CloudflareGalleryMediaStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareGalleryMediaStorageService> logger)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        _configuration = configuration;
        _publicUrl = GetCloudflareConfig(configuration, "PublicUrl") ?? throw new InvalidOperationException("Cloudflare R2 public URL not configured");
    }

    public async Task<string> UploadMediaAsync(Stream fileStream, string fileName, string contentType, MediaType mediaType,
        string? title = null, int? year = null, byte? month = null, byte? day = null)
    {
        try
        {
            // Build filename: currenttimestamp-title-yyyy_mm_dd.ext
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var extension = Path.GetExtension(fileName);
            var sanitizedTitle = !string.IsNullOrEmpty(title) ? SanitizeForFilename(title) : "media";

            var dateStr = "";
            if (year.HasValue)
            {
                dateStr = year.ToString();
                if (month.HasValue)
                {
                    dateStr += $"_{month:D2}";
                    if (day.HasValue)
                    {
                        dateStr += $"_{day:D2}";
                    }
                }
            }

            var finalFileName = string.IsNullOrEmpty(dateStr)
                ? $"{timestamp}-{sanitizedTitle}{extension}"
                : $"{timestamp}-{sanitizedTitle}-{dateStr}{extension}";

            var mediaTypeFolder = mediaType.ToString().ToLower();
            var key = $"images/{_environment}/gallery/{mediaTypeFolder}/{finalFileName}";

            // Copy to MemoryStream to make it seekable (required for S3 checksum calculation)
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = memoryStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
                UseChunkEncoding = false
            };

            // Add cache control headers for browser caching
            request.Headers.CacheControl = "public, max-age=31536000, immutable";

            // Add metadata
            request.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            request.Metadata.Add("x-amz-meta-media-type", mediaType.ToString());
            request.Metadata.Add("x-amz-meta-environment", _environment);

            await _s3Client.PutObjectAsync(request);

            return $"{_publicUrl}/{key}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading media file {FileName}", fileName);
            throw;
        }
    }

    public async Task DeleteMediaAsync(string mediaUrl)
    {
        // Ownership check, not a bare key extraction: on a DEV database cloned from production
        // this URL can point at the production bucket, which this environment must never delete
        // from. Anything not owned here is refused (and logged) and the reference is dropped.
        //
        // This also replaces a `mediaUrl.Replace($"{_publicUrl}/", "")`, which silently produced
        // the whole absolute URL as the "key" whenever the URL was not under _publicUrl - exactly
        // the inherited-production case - and then issued a delete with it.
        var key = ResolveDeletableKey(mediaUrl, nameof(DeleteMediaAsync));
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media file {MediaUrl}", mediaUrl);
            throw;
        }
    }

    public Task<string?> GenerateThumbnailAsync(string videoUrl)
    {
        // For now, return null - thumbnail generation can be implemented later
        // This would typically involve video processing with FFmpeg or similar
        _logger.LogWarning("Thumbnail generation not implemented yet for video {VideoUrl}", videoUrl);
        return Task.FromResult<string?>(null);
    }

    public long GetMaxFileSize(MediaType mediaType)
    {
        if (mediaType == MediaType.Video)
        {
            // Check for configured max video size
            var configuredSize = _configuration.GetValue<long?>("GalleryMedia:MaxVideoSize");
            return configuredSize ?? MaxVideoSizeDefault;
        }

        return MaxImageSize;
    }

    private string SanitizeForFilename(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "media";

        // Remove or replace invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');

        // Replace spaces with hyphens
        sanitized = sanitized.Replace(" ", "-");

        // Limit length
        if (sanitized.Length > 50)
            sanitized = sanitized.Substring(0, 50);

        return string.IsNullOrEmpty(sanitized) ? "media" : sanitized;
    }
}
