using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of image storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareImageStorageService : BaseCloudflareStorageService<CloudflareImageStorageService>, IImageStorageService
{
    private readonly string _publicBaseUrl;

    public CloudflareImageStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareImageStorageService> logger)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        // Get Cloudflare R2 public URL
        var publicUrl = GetCloudflareConfig(configuration, "PublicUrl");

        if (string.IsNullOrEmpty(publicUrl))
        {
            var errorMsg = "Cloudflare R2 public URL not configured. Set Cloudflare:R2:PublicUrl (e.g., https://pub-xxx.r2.dev).";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _publicBaseUrl = publicUrl.TrimEnd('/');
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string entityType, string entityId)
    {
        // Generate object key with timestamp for all entities
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var objectKey = $"images/{_environment}/{entityType}/{entityId}_{timestamp}.webp";

        // Additional metadata specific to image uploads
        var additionalMetadata = new Dictionary<string, string>
        {
            { "x-amz-meta-entity-type", entityType },
            { "x-amz-meta-entity-id", entityId }
        };

        return await UploadMediaAsync(fileStream, fileName, "image/webp", objectKey, _publicBaseUrl, additionalMetadata);
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            _logger.LogWarning("Attempted to delete image with empty URL");
            return;
        }

        // Extract object key from the public URL
        var objectKey = ExtractObjectKeyFromUrl(imageUrl);
        if (string.IsNullOrEmpty(objectKey))
        {
            _logger.LogWarning("Could not extract object key from URL: {ImageUrl}", imageUrl);
            return;
        }

        try
        {
            await DeleteObjectAsync(objectKey);
        }
        catch
        {
            // Don't throw - deletion failure shouldn't block operations
            // Error already logged in base class
        }
    }

    public async Task<bool> ImageExistsAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return false;

        var objectKey = ExtractObjectKeyFromUrl(imageUrl);
        if (string.IsNullOrEmpty(objectKey))
            return false;

        return await ObjectExistsAsync(objectKey);
    }
}
