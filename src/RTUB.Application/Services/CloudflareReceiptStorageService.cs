using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of receipt storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareReceiptStorageService : BaseCloudflareStorageService<CloudflareReceiptStorageService>, IReceiptStorageService
{
    private readonly string _publicBaseUrl;

    public CloudflareReceiptStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareReceiptStorageService> logger)
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

    public async Task<string> UploadReceiptAsync(Stream fileStream, string fileName, string contentType, int transactionId)
    {
        try
        {
            // Determine file extension based on content type
            var extension = GetFileExtension(contentType, fileName);
            
            // Generate object key with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var objectKey = $"receipts/{_environment}/{transactionId}_{timestamp}{extension}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
                UseChunkEncoding = false,
                DisablePayloadSigning = true // Required for non-seekable streams from Blazor file uploads
            };

            // Add cache control headers for browser caching
            // Since URLs include timestamp, they are immutable - cache for 1 year
            putRequest.Headers.CacheControl = "public, max-age=31536000, immutable";

            // Add metadata to help with debugging
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-transaction-id", transactionId.ToString());
            putRequest.Metadata.Add("x-amz-meta-environment", _environment);
            putRequest.Metadata.Add("x-amz-meta-original-filename", fileName);

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                var publicUrl = $"{_publicBaseUrl}/{objectKey}";

                return publicUrl;
            }
            else
            {
                var errorMsg = $"Failed to upload receipt. Status code: {response.HttpStatusCode}";
                _logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading receipt for transaction {TransactionId}. ErrorCode: {ErrorCode}, Message: {Message}",
                transactionId, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading receipt for transaction {TransactionId}", transactionId);
            throw;
        }
    }

    public async Task DeleteReceiptAsync(string receiptUrl)
    {
        if (string.IsNullOrEmpty(receiptUrl))
        {
            _logger.LogWarning("Attempted to delete receipt with empty URL");
            return;
        }

        // Extract object key from the public URL
        var objectKey = ExtractObjectKeyFromUrl(receiptUrl);
        if (string.IsNullOrEmpty(objectKey))
        {
            _logger.LogWarning("Could not extract object key from URL: {ReceiptUrl}", receiptUrl);
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

    public async Task<bool> ReceiptExistsAsync(string receiptUrl)
    {
        if (string.IsNullOrEmpty(receiptUrl))
            return false;

        var objectKey = ExtractObjectKeyFromUrl(receiptUrl);
        if (string.IsNullOrEmpty(objectKey))
            return false;

        return await ObjectExistsAsync(objectKey);
    }

    /// <summary>
    /// Gets the appropriate file extension based on content type and original filename
    /// </summary>
    private static string GetFileExtension(string contentType, string fileName)
    {
        // Check content type first
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            _ => Path.GetExtension(fileName)?.ToLowerInvariant() ?? ".bin"
        };
    }
}
