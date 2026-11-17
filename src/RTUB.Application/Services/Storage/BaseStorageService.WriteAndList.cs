// Base S3 storage: put, delete, list and URL-to-key helpers.
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services.Storage;

public abstract partial class BaseStorageService<TLogger>
{
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