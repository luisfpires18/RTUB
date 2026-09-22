using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of item type media storage using Cloudflare R2.
/// Handles images for weapon and drink type configurations.
/// </summary>
public class CloudflareItemTypeMediaStorageService
    : BaseCloudflareStorageService<CloudflareItemTypeMediaStorageService>, IItemTypeMediaStorageService
{
    private readonly string _publicBaseUrl;

    public CloudflareItemTypeMediaStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareItemTypeMediaStorageService> logger)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        var publicUrl = GetCloudflareConfig(configuration, "PublicUrl");

        if (string.IsNullOrEmpty(publicUrl))
        {
            var errorMsg = "Cloudflare R2 public URL not configured. Set Cloudflare:R2:PublicUrl.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _publicBaseUrl = publicUrl.TrimEnd('/');
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string itemTypeKey)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var sanitizedTypeKey = SanitizeFileName(itemTypeKey);
        var objectKey = $"item-configs/{_environment}/images/{sanitizedTypeKey}/{timestamp}_{sanitizedFileName}";

        var additionalMetadata = new Dictionary<string, string>
        {
            { "x-amz-meta-item-type-key", itemTypeKey },
            { "x-amz-meta-media-type", "image" }
        };

        return await UploadMediaAsync(fileStream, fileName, contentType, objectKey, _publicBaseUrl, additionalMetadata);
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            _logger.LogWarning("Attempted to delete item type image with empty URL");
            return;
        }

        // Ownership check, not a bare key extraction: on a DEV database cloned from production
        // this URL can point at the production bucket, which this environment must never delete
        // from. Anything not owned here is refused (and logged) and the reference is dropped.
        var objectKey = ResolveDeletableKey(imageUrl, nameof(DeleteImageAsync));
        if (string.IsNullOrEmpty(objectKey))
        {
            return;
        }

        try
        {
            await DeleteObjectAsync(objectKey);
        }
        catch
        {
            // Don't throw - deletion failure shouldn't block operations
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
