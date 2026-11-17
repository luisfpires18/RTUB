using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Base class for all storage services providing common S3 operations and error handling.
/// Contains shared logic for S3-compatible storage providers (Cloudflare R2)
/// </summary>
/// <typeparam name="TLogger">The logger type for the derived service</typeparam>
public abstract partial class BaseStorageService<TLogger>
{
    /// <summary>
    /// S3 client for performing storage operations
    /// </summary>
    protected readonly IAmazonS3 _s3Client;

    /// <summary>
    /// Logger for the storage service
    /// </summary>
    protected readonly ILogger<TLogger> _logger;

    /// <summary>
    /// Bucket name for storage operations
    /// </summary>
    protected readonly string _bucketName;

    /// <summary>
    /// Initializes a new instance of the BaseStorageService class
    /// </summary>
    /// <param name="s3Client">S3 client instance</param>
    /// <param name="bucketName">Bucket name for storage operations</param>
    /// <param name="logger">Logger instance</param>
    protected BaseStorageService(
        IAmazonS3 s3Client,
        string bucketName,
        ILogger<TLogger> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if an object exists in S3 storage
    /// </summary>
    /// <param name="objectKey">The object key to check</param>
    /// <returns>True if the object exists, false otherwise</returns>
    protected async Task<bool> ObjectExistsAsync(string objectKey)
    {
        try
        {
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
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error checking object existence. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking if object exists: {ObjectKey}", objectKey);
            return false;
        }
    }

    /// <summary>
    /// Generates a pre-signed URL for accessing an S3 object
    /// </summary>
    /// <param name="objectKey">The object key</param>
    /// <param name="expirationMinutes">URL expiration time in minutes</param>
    /// <param name="responseHeaderOverrides">Optional response header overrides</param>
    /// <returns>The pre-signed URL, or null if the object doesn't exist or an error occurs</returns>
    protected async Task<string?> GeneratePreSignedUrlAsync(
        string objectKey,
        int expirationMinutes,
        ResponseHeaderOverrides? responseHeaderOverrides = null)
    {
        try
        {
            // Check if file exists first
            var exists = await ObjectExistsAsync(objectKey);
            if (!exists)
            {
                _logger.LogWarning("Cannot generate URL - object not found: {ObjectKey}", objectKey);
                return null;
            }

            // Generate pre-signed URL
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            if (responseHeaderOverrides != null)
            {
                request.ResponseHeaderOverrides = responseHeaderOverrides;
            }

            var url = _s3Client.GetPreSignedURL(request);
            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating pre-signed URL. ObjectKey: {ObjectKey}, ErrorCode: {ErrorCode}, Message: {Message}",
                objectKey, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating pre-signed URL for: {ObjectKey}", objectKey);
            return null;
        }
    }

    /// <summary>
    /// Gets the metadata for an S3 object including its size
    /// </summary>
    /// <param name="objectKey">The object key</param>
    /// <returns>The object's content length in bytes, or 0 if not found</returns>
    protected async Task<long> GetObjectSizeAsync(string objectKey)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            var response = await _s3Client.GetObjectMetadataAsync(request);
            return response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Object not found when getting size: {ObjectKey}", objectKey);
            return 0;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error getting object size. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting object size for: {ObjectKey}", objectKey);
            return 0;
        }
    }

    /// <summary>
    /// Gets the last-modified timestamp (UTC) for an S3 object
    /// </summary>
    /// <param name="objectKey">The object key</param>
    /// <returns>The last-modified timestamp in UTC, or null if the object does not exist</returns>
    protected async Task<DateTime?> GetObjectLastModifiedUtcAsync(string objectKey)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            var response = await _s3Client.GetObjectMetadataAsync(request);
            return response.LastModified?.ToUniversalTime();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error getting object last-modified. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting object last-modified for: {ObjectKey}", objectKey);
            return null;
        }
    }

    /// <summary>
    /// Copies an object within the bucket server-side, without downloading or re-uploading it
    /// </summary>
    /// <param name="sourceKey">The source object key</param>
    /// <param name="destinationKey">The destination object key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = _bucketName,
                SourceKey = sourceKey,
                DestinationBucket = _bucketName,
                DestinationKey = destinationKey
            };

            await _s3Client.CopyObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error copying object. Bucket: '{BucketName}', Source: '{SourceKey}', Destination: '{DestinationKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, sourceKey, destinationKey, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error copying object from {SourceKey} to {DestinationKey}", sourceKey, destinationKey);
            throw;
        }
    }
}
