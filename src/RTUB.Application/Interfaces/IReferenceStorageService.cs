namespace RTUB.Application.Interfaces;

/// <summary>
/// Read-only access to the production bucket, for a non-production environment running on a
/// sanitized snapshot of the production database.
/// </summary>
/// <remarks>
/// <para>Deliberately has no upload, delete, copy or move member, and no implementation of it is
/// allowed to grow one. The normal storage services keep read/write/delete against this
/// environment's own bucket; this is the only way to reach the production bucket, and it can
/// only read. A bug in application code therefore cannot mutate a production object through it,
/// independently of what the configured credential happens to permit.</para>
///
/// <para>Only private files need this. Public media is served straight from its absolute URL and
/// needs no credential at all - see the CSP reference origin instead.</para>
/// </remarks>
public interface IReferenceStorageService
{
    /// <summary>
    /// Whether a production reference bucket is configured at all. False in production, and false
    /// in any environment that has not been given the dedicated read-only credential.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Whether an object exists in the production reference bucket.</summary>
    Task<bool> ObjectExistsAsync(string objectKey);

    /// <summary>
    /// A time-limited pre-signed GET URL for an object in the production reference bucket, or
    /// null when it is not configured or the object does not exist.
    /// </summary>
    /// <param name="objectKey">The object key</param>
    /// <param name="expirationMinutes">URL lifetime in minutes</param>
    /// <param name="contentType">Optional Content-Type override on the response</param>
    /// <param name="contentDisposition">Optional Content-Disposition override on the response</param>
    Task<string?> GetPreSignedUrlAsync(
        string objectKey,
        int expirationMinutes,
        string? contentType = null,
        string? contentDisposition = null);
}
