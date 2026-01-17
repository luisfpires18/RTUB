using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
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
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var sanitizedFileName = SanitizeFileName(fileName);
            var sanitizedInstrument = SanitizeFileName(instrumentType);
            var objectKey = $"naipes/{_environment}/videos/{sanitizedInstrument}/{timestamp}_{sanitizedFileName}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
                UseChunkEncoding = false
            };

            // Cache control for immutable resources
            putRequest.Headers.CacheControl = "public, max-age=31536000, immutable";

            // Metadata
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-instrument-type", instrumentType);
            putRequest.Metadata.Add("x-amz-meta-media-type", "video");
            putRequest.Metadata.Add("x-amz-meta-environment", _environment);

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                return $"{_publicBaseUrl}/{objectKey}";
            }
            else
            {
                var errorMsg = $"Failed to upload video. Status code: {response.HttpStatusCode}";
                _logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading video for instrument {InstrumentType}. ErrorCode: {ErrorCode}, Message: {Message}",
                instrumentType, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading video for instrument {InstrumentType}", instrumentType);
            throw;
        }
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string instrumentType)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var sanitizedFileName = SanitizeFileName(fileName);
            var sanitizedInstrument = SanitizeFileName(instrumentType);
            var objectKey = $"naipes/{_environment}/images/{sanitizedInstrument}/{timestamp}_{sanitizedFileName}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
                UseChunkEncoding = false
            };

            // Cache control for immutable resources
            putRequest.Headers.CacheControl = "public, max-age=31536000, immutable";

            // Metadata
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-instrument-type", instrumentType);
            putRequest.Metadata.Add("x-amz-meta-media-type", "image");
            putRequest.Metadata.Add("x-amz-meta-environment", _environment);

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                return $"{_publicBaseUrl}/{objectKey}";
            }
            else
            {
                var errorMsg = $"Failed to upload image. Status code: {response.HttpStatusCode}";
                _logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading image for instrument {InstrumentType}. ErrorCode: {ErrorCode}, Message: {Message}",
                instrumentType, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading image for instrument {InstrumentType}", instrumentType);
            throw;
        }
    }

    public async Task DeleteMediaAsync(string mediaUrl)
    {
        if (string.IsNullOrEmpty(mediaUrl))
        {
            _logger.LogWarning("Attempted to delete media with empty URL");
            return;
        }

        var objectKey = ExtractObjectKeyFromUrl(mediaUrl);
        if (string.IsNullOrEmpty(objectKey))
        {
            _logger.LogWarning("Could not extract object key from URL: {MediaUrl}", mediaUrl);
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

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid characters and keep only alphanumeric, dots, hyphens, and underscores
        var sanitized = string.Concat(fileName.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'));
        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }
}
