using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of event video storage service using Cloudflare R2
/// Handles video uploads and deletions for events
/// </summary>
public class CloudflareEventVideoStorageService : BaseCloudflareStorageService<CloudflareEventVideoStorageService>, IEventVideoStorageService
{
    private readonly string _publicBaseUrl;

    public CloudflareEventVideoStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareEventVideoStorageService> logger)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        // Get Cloudflare R2 public URL
        var publicUrl = GetCloudflareConfig(configuration, "PublicUrl");

        if (string.IsNullOrEmpty(publicUrl))
        {
            var errorMsg = "Cloudflare R2 public URL not configured. Set Cloudflare:R2:PublicUrl.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _publicBaseUrl = publicUrl.TrimEnd('/');
    }

    public async Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, int eventId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var objectKey = $"events/{_environment}/videos/{eventId}_{timestamp}_{sanitizedFileName}";

        // Additional metadata specific to video uploads
        var additionalMetadata = new Dictionary<string, string>
        {
            { "x-amz-meta-event-id", eventId.ToString() }
        };

        return await UploadMediaAsync(fileStream, fileName, contentType, objectKey, _publicBaseUrl, additionalMetadata);
    }

    public async Task DeleteVideoAsync(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl))
        {
            _logger.LogWarning("Attempted to delete video with empty URL");
            return;
        }

        var objectKey = ExtractObjectKeyFromUrl(videoUrl);
        if (string.IsNullOrEmpty(objectKey))
        {
            _logger.LogWarning("Could not extract object key from URL: {VideoUrl}", videoUrl);
            return;
        }

        try
        {
            await DeleteObjectAsync(objectKey);
        }
        catch (Exception ex)
        {
            // Don't throw - deletion failure shouldn't block operations
            // Log for diagnostic purposes
            _logger.LogWarning(ex, "Failed to delete video from storage: {VideoUrl}", videoUrl);
        }
    }

    public async Task<bool> VideoExistsAsync(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl))
            return false;

        var objectKey = ExtractObjectKeyFromUrl(videoUrl);
        if (string.IsNullOrEmpty(objectKey))
            return false;

        return await ObjectExistsAsync(objectKey);
    }
}
