using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Base class for Cloudflare R2 storage services that use dependency-injected S3 client.
/// Provides common configuration retrieval and initialization for Cloudflare R2 services.
/// </summary>
/// <typeparam name="TLogger">The logger type for the derived service</typeparam>
public abstract class BaseCloudflareStorageService<TLogger> : BaseStorageService<TLogger>
{
    /// <summary>
    /// The current hosting environment name (e.g., Production, Development)
    /// </summary>
    protected readonly string _environment;

    /// <summary>
    /// Initializes a new instance of the BaseCloudflareStorageService class
    /// </summary>
    /// <param name="s3Client">Injected S3 client instance</param>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="hostEnvironment">Host environment instance</param>
    /// <param name="logger">Logger instance</param>
    protected BaseCloudflareStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<TLogger> logger)
        : base(s3Client, GetBucketName(configuration, logger), logger)
    {
        _environment = hostEnvironment.EnvironmentName;
    }

    /// <summary>
    /// Retrieves and validates the Cloudflare R2 bucket name from configuration
    /// </summary>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="logger">Logger instance for error reporting</param>
    /// <returns>The validated bucket name</returns>
    /// <exception cref="InvalidOperationException">Thrown when bucket name is not configured</exception>
    private static string GetBucketName(IConfiguration configuration, ILogger<TLogger> logger)
    {
        var bucketName = configuration["Cloudflare:R2:Bucket"];

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = "Cloudflare R2 bucket name not configured. Set Cloudflare:R2:Bucket.";
            logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        return bucketName;
    }

    /// <summary>
    /// Gets a configuration value from the Cloudflare R2 section
    /// </summary>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value or null if not found</returns>
    protected static string? GetCloudflareConfig(IConfiguration configuration, string key)
    {
        return configuration[$"Cloudflare:R2:{key}"];
    }

    /// <summary>
    /// Generic helper method for uploading media files to Cloudflare R2
    /// Consolidates common upload logic used across multiple storage services
    /// </summary>
    /// <param name="fileStream">Stream containing the file data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="objectKey">Full S3 object key (path) for the file</param>
    /// <param name="publicBaseUrl">Public base URL for constructing the returned URL</param>
    /// <param name="additionalMetadata">Optional dictionary of additional metadata to add</param>
    /// <returns>Public URL of the uploaded file</returns>
    /// <exception cref="Exception">Thrown when upload fails</exception>
    protected async Task<string> UploadMediaAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string objectKey,
        string publicBaseUrl,
        Dictionary<string, string>? additionalMetadata = null)
    {
        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
                UseChunkEncoding = false
            };

            // Cache control for immutable resources (URLs include timestamp)
            putRequest.Headers.CacheControl = "public, max-age=31536000, immutable";

            // Standard metadata
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-environment", _environment);
            putRequest.Metadata.Add("x-amz-meta-original-filename", fileName);

            // Add additional metadata if provided
            if (additionalMetadata != null)
            {
                foreach (var kvp in additionalMetadata)
                {
                    putRequest.Metadata.Add(kvp.Key, kvp.Value);
                }
            }

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                return $"{publicBaseUrl.TrimEnd('/')}/{objectKey}";
            }
            else
            {
                var errorMsg = $"Failed to upload media. Status code: {response.HttpStatusCode}";
                _logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading media. ObjectKey: {ObjectKey}, ErrorCode: {ErrorCode}, Message: {Message}",
                objectKey, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading media: {ObjectKey}", objectKey);
            throw;
        }
    }

    /// <summary>
    /// Sanitizes a filename by removing invalid characters
    /// Keeps only alphanumeric characters, dots, hyphens, and underscores
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <returns>Sanitized filename, or "file" if the result would be empty</returns>
    protected static string SanitizeFileName(string fileName)
    {
        var sanitized = string.Concat(fileName.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'));
        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }
}
