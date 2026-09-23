using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of naipe media storage service using Cloudflare R2
/// Handles videos and images for instrument learning materials
/// </summary>
public class CloudflareNaipeMediaStorageService : BaseCloudflareStorageService<CloudflareNaipeMediaStorageService>, INaipeMediaStorageService
{
    private readonly string _publicBaseUrl;

    public CloudflareNaipeMediaStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareNaipeMediaStorageService> logger)
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

    public async Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, string instrumentType)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var sanitizedInstrument = SanitizeFileName(instrumentType);
        var objectKey = $"naipes/{_environment}/videos/{sanitizedInstrument}/{timestamp}_{sanitizedFileName}";

        var additionalMetadata = new Dictionary<string, string>
        {
            { "x-amz-meta-instrument-type", instrumentType },
            { "x-amz-meta-media-type", "video" }
        };

        return await UploadMediaAsync(fileStream, fileName, contentType, objectKey, _publicBaseUrl, additionalMetadata);
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string instrumentType)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var sanitizedInstrument = SanitizeFileName(instrumentType);
        var objectKey = $"naipes/{_environment}/images/{sanitizedInstrument}/{timestamp}_{sanitizedFileName}";

        var additionalMetadata = new Dictionary<string, string>
        {
            { "x-amz-meta-instrument-type", instrumentType },
            { "x-amz-meta-media-type", "image" }
        };

        return await UploadMediaAsync(fileStream, fileName, contentType, objectKey, _publicBaseUrl, additionalMetadata);
    }

    public async Task DeleteMediaAsync(string mediaUrl)
    {
        if (string.IsNullOrEmpty(mediaUrl))
        {
            _logger.LogWarning("Attempted to delete media with empty URL");
            return;
        }

        // Ownership check, not a bare key extraction: on a DEV database cloned from production
        // this URL can point at the production bucket, which this environment must never delete
        // from. Anything not owned here is refused (and logged) and the reference is dropped.
        var objectKey = ResolveDeletableKey(mediaUrl, nameof(DeleteMediaAsync));
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

    public async Task<bool> MediaExistsAsync(string mediaUrl)
    {
        if (string.IsNullOrEmpty(mediaUrl))
            return false;

        var objectKey = ExtractObjectKeyFromUrl(mediaUrl);
        if (string.IsNullOrEmpty(objectKey))
            return false;

        return await ObjectExistsAsync(objectKey);
    }

}
