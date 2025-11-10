using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of image storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareImageStorageService : IImageStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;
    private readonly string _environment;
    private readonly ILogger<CloudflareImageStorageService> _logger;

    public CloudflareImageStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareImageStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger;

        // Get Cloudflare R2 configuration
        var bucketName = configuration["Cloudflare:R2:Bucket"];
        var publicUrl = configuration["Cloudflare:R2:PublicUrl"];

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = "Cloudflare R2 bucket name not configured. Set Cloudflare:R2:Bucket.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(publicUrl))
        {
            var errorMsg = "Cloudflare R2 public URL not configured. Set Cloudflare:R2:PublicUrl (e.g., https://pub-xxx.r2.dev).";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _bucketName = bucketName;
        _publicBaseUrl = publicUrl.TrimEnd('/');
        _environment = hostEnvironment.EnvironmentName;
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string entityType, string entityId)
    {
        try
        {
            // Generate object key: {environment}/{entityType}/{entityId}_{timestamp}.webp
            // e.g., Development/albums/123_20241110120000.webp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var objectKey = $"images/{_environment}/{entityType}/{entityId}_{timestamp}.webp";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = "image/webp", // Always use webp format
                // Make the object publicly accessible (no expiry)
                CannedACL = S3CannedACL.PublicRead,
                // Required for Cloudflare R2 to avoid TLS/handshake errors
                UseChunkEncoding = false
            };

            // Add metadata to help with debugging
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-entity-type", entityType);
            putRequest.Metadata.Add("x-amz-meta-entity-id", entityId);
            putRequest.Metadata.Add("x-amz-meta-environment", _environment);

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                var publicUrl = $"{_publicBaseUrl}/{objectKey}";

                return publicUrl;
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
            _logger.LogError(ex, "S3 error uploading image for {EntityType}/{EntityId}. ErrorCode: {ErrorCode}, Message: {Message}",
                entityType, entityId, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading image for {EntityType}/{EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        try
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

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error deleting image {ImageUrl}. ErrorCode: {ErrorCode}, Message: {Message}",
                imageUrl, ex.ErrorCode, ex.Message);
            // Don't throw - deletion failure shouldn't block operations
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting image {ImageUrl}", imageUrl);
            // Don't throw - deletion failure shouldn't block operations
        }
    }

    public async Task<bool> ImageExistsAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
                return false;

            var objectKey = ExtractObjectKeyFromUrl(imageUrl);
            if (string.IsNullOrEmpty(objectKey))
                return false;

            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error checking image existence {ImageUrl}. ErrorCode: {ErrorCode}, Message: {Message}",
                imageUrl, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking if image exists: {ImageUrl}", imageUrl);
            return false;
        }
    }



    /// <summary>
    /// Extract object key from public URL
    /// </summary>
    private string? ExtractObjectKeyFromUrl(string imageUrl)
    {
        try
        {
            // URL format: https://pub-xxx.r2.dev/{objectKey}
            // We need to extract the objectKey part (everything after the domain)
            var uri = new Uri(imageUrl);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // All segments form the object key (no bucket name in path)
            if (pathSegments.Length > 0)
            {
                return string.Join("/", pathSegments);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting object key from URL: {ImageUrl}", imageUrl);
            return null;
        }
    }
}
