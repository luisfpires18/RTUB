using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<CloudflareImageStorageService> _logger;

    public CloudflareImageStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<CloudflareImageStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger;

        // Get Cloudflare R2 configuration
        var accountId = configuration["Cloudflare:R2:AccountId"];
        var bucketName = configuration["Cloudflare:R2:Bucket"];

        if (string.IsNullOrEmpty(accountId))
        {
            var errorMsg = "Cloudflare R2 account ID not configured. Set Cloudflare:R2:AccountId.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = "Cloudflare R2 bucket name not configured. Set Cloudflare:R2:Bucket.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _bucketName = bucketName;
        _publicBaseUrl = $"https://pub-{accountId}.r2.dev/{bucketName}";

        _logger.LogInformation("Cloudflare R2 image storage service initialized for bucket: {BucketName}", _bucketName);
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string entityType, string entityId)
    {
        try
        {
            // Generate object key: images/{entityType}/{entityId}/{sanitized-filename}
            var sanitizedFileName = SanitizeFileName(fileName);
            var objectKey = $"images/{entityType}/{entityId}/{sanitizedFileName}";

            _logger.LogInformation("Uploading image to R2: {ObjectKey}", objectKey);

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType,
                // Make the object publicly accessible (no expiry)
                CannedACL = S3CannedACL.PublicRead,
                // Required for Cloudflare R2 to avoid TLS/handshake errors
                UseChunkEncoding = false
            };

            // Add metadata to help with debugging
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-entity-type", entityType);
            putRequest.Metadata.Add("x-amz-meta-entity-id", entityId);

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                var publicUrl = $"{_publicBaseUrl}/{objectKey}";
                _logger.LogInformation("Successfully uploaded image: {PublicUrl}", publicUrl);
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

            _logger.LogInformation("Deleting image from R2: {ObjectKey}", objectKey);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);
            _logger.LogInformation("Successfully deleted image: {ObjectKey}", objectKey);
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
    /// Sanitize filename to be safe for S3 object keys
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        // Remove path information
        fileName = Path.GetFileName(fileName);
        
        // Replace spaces and special characters
        fileName = fileName.Replace(" ", "_");
        
        // Keep only alphanumeric, dash, underscore, and dot
        fileName = new string(fileName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.').ToArray());
        
        // Add timestamp to ensure uniqueness
        var extension = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        
        return $"{nameWithoutExt}_{timestamp}{extension}";
    }

    /// <summary>
    /// Extract object key from public URL
    /// </summary>
    private string? ExtractObjectKeyFromUrl(string imageUrl)
    {
        try
        {
            // URL format: https://pub-{accountId}.r2.dev/{bucket}/{objectKey}
            // We need to extract the objectKey part
            var uri = new Uri(imageUrl);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // First segment is bucket name, rest is the object key
            if (pathSegments.Length > 1)
            {
                return string.Join("/", pathSegments.Skip(1));
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
