using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Cloudflare R2 storage for SQLite database backups.
/// </summary>
/// <remarks>
/// Derives from <see cref="BaseStorageService{TLogger}"/> rather than
/// BaseCloudflareStorageService on purpose: that base reads the media bucket name and its
/// upload helper forces a public-read ACL and an immutable cache-control header, neither
/// of which belongs on a database dump.
/// </remarks>
public class DatabaseBackupStorageService : BaseStorageService<DatabaseBackupStorageService>, IDatabaseBackupStorageService
{
    public DatabaseBackupStorageService(
        IAmazonS3 s3Client,
        IOptions<DatabaseBackupOptions> options,
        ILogger<DatabaseBackupStorageService> logger)
        : base(s3Client, GetBucketName(options, logger), logger)
    {
    }

    private static string GetBucketName(IOptions<DatabaseBackupOptions> options, ILogger<DatabaseBackupStorageService> logger)
    {
        var bucket = options.Value.Bucket;

        if (string.IsNullOrWhiteSpace(bucket))
        {
            var errorMsg = "Database backup bucket not configured. Set DatabaseBackup:Bucket.";
            logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        return bucket;
    }

    /// <inheritdoc />
    public async Task UploadFileAsync(string objectKey, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object key is required.", nameof(objectKey));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Backup file to upload was not found.", filePath);

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                FilePath = filePath,
                ContentType = "application/x-sqlite3",
                // Required for Cloudflare R2 compatibility, as in the media storage services.
                UseChunkEncoding = false
            };

            // Backups must never be cached by anything in front of the bucket.
            request.Headers.CacheControl = "no-store";
            request.Metadata.Add("x-amz-meta-created-at", DateTime.UtcNow.ToString("o"));

            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading backup. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}",
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
        => CopyObjectAsync(sourceKey, destinationKey, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        => DeleteObjectAsync(objectKey);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string objectKey) => ObjectExistsAsync(objectKey);

    /// <inheritdoc />
    public Task<long> GetSizeAsync(string objectKey) => GetObjectSizeAsync(objectKey);

    /// <inheritdoc />
    public Task<DateTime?> GetLastModifiedUtcAsync(string objectKey) => GetObjectLastModifiedUtcAsync(objectKey);
}
