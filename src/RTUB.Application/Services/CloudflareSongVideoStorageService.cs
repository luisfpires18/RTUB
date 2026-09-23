using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of song video storage service using Cloudflare R2
/// Handles video uploads and deletions for songs
/// </summary>
public class CloudflareSongVideoStorageService : BaseCloudflareStorageService<CloudflareSongVideoStorageService>, ISongVideoStorageService
{
    private readonly string _publicBaseUrl;

    public CloudflareSongVideoStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareSongVideoStorageService> logger)
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

    public async Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, int songId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var objectKey = $"songs/{_environment}/videos/{songId}_{timestamp}_{sanitizedFileName}";

        var additionalMetadata = new Dictionary<string, string>
        {
            { "x-amz-meta-song-id", songId.ToString() }
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

        // Ownership check, not a bare key extraction: on a DEV database cloned from production
        // this URL can point at the production bucket, which this environment must never delete
        // from. Anything not owned here is refused (and logged) and the reference is dropped.
        var objectKey = ResolveDeletableKey(videoUrl, nameof(DeleteVideoAsync));
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
