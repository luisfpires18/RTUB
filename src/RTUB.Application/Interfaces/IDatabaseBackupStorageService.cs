namespace RTUB.Application.Interfaces;

/// <summary>
/// Storage operations for SQLite database backups held in a private Cloudflare R2 bucket.
/// Deliberately separate from the media storage services: backups are never public, never
/// cached, and are expected to use a bucket-scoped credential of their own.
/// </summary>
public interface IDatabaseBackupStorageService
{
    /// <summary>
    /// Uploads a local file to the given object key.
    /// </summary>
    /// <param name="objectKey">Destination object key</param>
    /// <param name="filePath">Path of the local file to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UploadFileAsync(string objectKey, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side copy between two object keys in the backup bucket. Does not re-upload.
    /// </summary>
    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object.
    /// </summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether an object exists.
    /// </summary>
    Task<bool> ExistsAsync(string objectKey);

    /// <summary>
    /// Size of an object in bytes, or 0 when it does not exist.
    /// </summary>
    Task<long> GetSizeAsync(string objectKey);

    /// <summary>
    /// Last-modified timestamp (UTC) of an object, or null when it does not exist.
    /// </summary>
    Task<DateTime?> GetLastModifiedUtcAsync(string objectKey);
}
