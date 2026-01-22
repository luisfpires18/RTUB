using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Base class for all storage services providing common S3 operations and error handling.
/// Contains shared logic for S3-compatible storage providers (Cloudflare R2, iDrive e2, etc.)
/// </summary>
/// <typeparam name="TLogger">The logger type for the derived service</typeparam>
public abstract class BaseStorageService<TLogger>
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
    /// Uploads an object to S3 storage
    /// </summary>
    /// <param name="objectKey">The object key</param>
    /// <param name="stream">The stream containing the object data</param>
    /// <param name="contentType">The content type of the object</param>
    /// <param name="additionalConfig">Optional action to configure additional PutObjectRequest properties</param>
    /// <returns>The HTTP status code of the upload operation</returns>
    protected async Task<HttpStatusCode> PutObjectAsync(
        string objectKey,
        Stream stream,
        string contentType,
        Action<PutObjectRequest>? additionalConfig = null)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType
            };

            // Allow derived classes to configure additional properties
            additionalConfig?.Invoke(request);

            var response = await _s3Client.PutObjectAsync(request);
            return response.HttpStatusCode;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading object. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading object: {ObjectKey}", objectKey);
            throw;
        }
    }

    /// <summary>
    /// Deletes an object from S3 storage
    /// </summary>
    /// <param name="objectKey">The object key to delete</param>
    protected async Task DeleteObjectAsync(string objectKey)
    {
        try
        {
            if (string.IsNullOrEmpty(objectKey))
            {
                _logger.LogWarning("Attempted to delete object with empty key");
                return;
            }

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(request);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error deleting object. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting object: {ObjectKey}", objectKey);
            throw;
        }
    }

    /// <summary>
    /// Lists all objects with a given prefix, paginating through all results
    /// </summary>
    /// <param name="prefix">The prefix to filter objects</param>
    /// <param name="delimiter">Optional delimiter for hierarchical listing</param>
    /// <returns>List of S3 objects matching the prefix</returns>
    /// <exception cref="AmazonS3Exception">Thrown when S3 operation fails</exception>
    protected async Task<List<S3Object>> ListObjectsAsync(string prefix, string? delimiter = null)
    {
        var objects = new List<S3Object>();
        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = prefix,
            Delimiter = delimiter
        };

        ListObjectsV2Response response;
        do
        {
            response = await _s3Client.ListObjectsV2Async(request);
            objects.AddRange(response.S3Objects);
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);

        return objects;
    }

    /// <summary>
    /// Lists common prefixes (folders) with a given prefix
    /// </summary>
    /// <param name="prefix">The prefix to filter folders</param>
    /// <param name="delimiter">Delimiter for hierarchical listing (default is "/")</param>
    /// <returns>List of common prefixes (folder paths)</returns>
    /// <exception cref="AmazonS3Exception">Thrown when S3 operation fails</exception>
    protected async Task<List<string>> ListCommonPrefixesAsync(string prefix, string delimiter = "/")
    {
        var prefixes = new HashSet<string>();
        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = prefix,
            Delimiter = delimiter
        };

        ListObjectsV2Response response;
        do
        {
            response = await _s3Client.ListObjectsV2Async(request);

            if (response.CommonPrefixes != null)
            {
                foreach (var commonPrefix in response.CommonPrefixes)
                {
                    prefixes.Add(commonPrefix);
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);

        return prefixes.OrderBy(p => p).ToList();
    }

    /// <summary>
    /// Deletes multiple objects in a single batch operation
    /// </summary>
    /// <param name="objectKeys">List of object keys to delete</param>
    /// <param name="maxBatchSize">Maximum number of objects per batch (S3 limit is 1000)</param>
    protected async Task DeleteObjectsBatchAsync(List<string> objectKeys, int maxBatchSize = 1000)
    {
        try
        {
            if (objectKeys == null || objectKeys.Count == 0)
                return;

            // Delete in batches
            for (int i = 0; i < objectKeys.Count; i += maxBatchSize)
            {
                var batch = objectKeys.Skip(i).Take(maxBatchSize).ToList();

                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = batch.Select(key => new KeyVersion { Key = key }).ToList()
                };

                await _s3Client.DeleteObjectsAsync(deleteRequest);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error batch deleting objects. Bucket: '{BucketName}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error batch deleting objects");
            throw;
        }
    }

    /// <summary>
    /// Extracts the object key from a public URL
    /// </summary>
    /// <param name="url">The full URL</param>
    /// <returns>The object key, or null if extraction fails</returns>
    protected string? ExtractObjectKeyFromUrl(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var uri = new Uri(url);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathSegments.Length > 0)
            {
                return string.Join("/", pathSegments);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting object key from URL: {Url}", url);
            return null;
        }
    }
}
