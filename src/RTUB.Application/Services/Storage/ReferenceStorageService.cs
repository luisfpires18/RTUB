using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Read-only view of the production R2 bucket, used by a Development/Staging app running on a
/// sanitized snapshot of the production database so that private files inherited from production
/// (documents, album audio, lyric PDFs) can still be opened.
/// </summary>
/// <remarks>
/// <para>Deliberately does <b>not</b> derive from <see cref="BaseStorageService{TLogger}"/>. That
/// base carries <c>PutObjectAsync</c>, <c>DeleteObjectAsync</c>, <c>DeleteObjectsBatchAsync</c>
/// and <c>CopyObjectAsync</c>, and inheriting it would put all four on a type whose entire
/// purpose is that it cannot mutate the production bucket. This class calls exactly two S3
/// operations - <c>GetObjectMetadata</c> and <c>GetPreSignedURL</c> - and there is no third.</para>
///
/// <para>Two independent gates keep it off in production: it refuses to configure itself when
/// <see cref="IHostEnvironment.IsProduction"/>, and it needs a dedicated
/// <c>Cloudflare:R2:Reference:*</c> credential that production never sets. Unconfigured, it
/// builds no S3 client, opens no connection and answers "not found" to everything.</para>
///
/// <para>The credential must be a read-only token scoped to the production bucket. The production
/// application's own write token must never be used here - see
/// <c>docs/cloudflare-r2-and-database-backups.md</c>.</para>
/// </remarks>
public sealed class ReferenceStorageService : IReferenceStorageService, IDisposable
{
    public const string SectionName = "Cloudflare:R2:Reference";

    private readonly ILogger<ReferenceStorageService> _logger;
    private readonly IAmazonS3? _s3Client;
    private readonly string? _bucketName;

    public ReferenceStorageService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<ReferenceStorageService> logger)
    {
        _logger = logger;

        // Gate one: never in production. A production app has no business holding a second client
        // for its own bucket, and this removes any path to one by misconfiguration.
        if (hostEnvironment.IsProduction())
        {
            return;
        }

        var accountId = configuration[$"{SectionName}:AccountId"];
        var accessKeyId = configuration[$"{SectionName}:AccessKeyId"];
        var secretAccessKey = configuration[$"{SectionName}:SecretAccessKey"];
        var bucket = configuration[$"{SectionName}:Bucket"];

        // Gate two: all four, or nothing. A partially configured reference is a misconfiguration,
        // not a degraded mode.
        if (string.IsNullOrWhiteSpace(accountId) ||
            string.IsNullOrWhiteSpace(accessKeyId) ||
            string.IsNullOrWhiteSpace(secretAccessKey) ||
            string.IsNullOrWhiteSpace(bucket))
        {
            return;
        }

        _bucketName = bucket;
        _s3Client = new AmazonS3Client(
            new BasicAWSCredentials(accessKeyId, secretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                AuthenticationRegion = "auto"
            });

        _logger.LogInformation(
            "Production reference storage is enabled in environment '{Environment}': bucket '{Bucket}', READ-ONLY.",
            hostEnvironment.EnvironmentName, bucket);
    }

    /// <inheritdoc />
    public bool IsConfigured => _s3Client != null;

    /// <inheritdoc />
    public async Task<bool> ObjectExistsAsync(string objectKey)
    {
        if (_s3Client == null || string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        try
        {
            await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            });

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            // A reference lookup is a convenience on a development box; it must never take a
            // request down, and the caller falls back to "not available".
            _logger.LogWarning(ex, "Reference storage lookup failed for key '{ObjectKey}'", objectKey);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetPreSignedUrlAsync(
        string objectKey,
        int expirationMinutes,
        string? contentType = null,
        string? contentDisposition = null)
    {
        if (_s3Client == null || string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        if (!await ObjectExistsAsync(objectKey))
        {
            return null;
        }

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                // GET only. A pre-signed PUT or DELETE is never produced here.
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            if (contentType != null || contentDisposition != null)
            {
                request.ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = contentType,
                    ContentDisposition = contentDisposition
                };
            }

            return _s3Client.GetPreSignedURL(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not pre-sign reference object '{ObjectKey}'", objectKey);
            return null;
        }
    }

    public void Dispose() => _s3Client?.Dispose();
}
