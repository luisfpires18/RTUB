using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Base class for iDrive e2 storage services that create their own S3 client.
/// Provides common configuration retrieval and S3 client initialization for iDrive e2 services.
/// </summary>
/// <typeparam name="TLogger">The logger type for the derived service</typeparam>
public abstract class BaseDriveStorageService<TLogger> : BaseStorageService<TLogger>, IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the BaseDriveStorageService class
    /// </summary>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="logger">Logger instance</param>
    protected BaseDriveStorageService(
        IConfiguration configuration,
        ILogger<TLogger> logger)
        : base(CreateS3Client(configuration, logger), GetBucketName(configuration, logger), logger)
    {
    }

    /// <summary>
    /// Creates and configures an S3 client for iDrive e2
    /// </summary>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="logger">Logger instance for error reporting</param>
    /// <returns>Configured S3 client</returns>
    /// <exception cref="InvalidOperationException">Thrown when credentials or endpoint are not configured</exception>
    private static IAmazonS3 CreateS3Client(IConfiguration configuration, ILogger<TLogger> logger)
    {
        // Get credentials from environment variables or configuration
        var accessKey = Environment.GetEnvironmentVariable("IDRIVE_ACCESS_KEY")
                        ?? configuration["IDrive:AccessKey"];
        var secretKey = Environment.GetEnvironmentVariable("IDRIVE_SECRET_KEY")
                        ?? configuration["IDrive:SecretKey"];
        var endpoint = Environment.GetEnvironmentVariable("IDRIVE_ENDPOINT")
                       ?? configuration["IDrive:Endpoint"]
                       ?? "s3.eu-west-4.idrivee2.com";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = "iDrive e2 credentials not configured. Set IDRIVE_ACCESS_KEY and IDRIVE_SECRET_KEY environment variables or IDrive:AccessKey and IDrive:SecretKey configuration.";
            logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{endpoint}",
            ForcePathStyle = true // Required for S3-compatible services
        };

        return new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Retrieves the iDrive e2 bucket name from environment variables or configuration
    /// </summary>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="logger">Logger instance (unused - kept for consistency)</param>
    /// <returns>The bucket name (defaults to "rtub" if not configured)</returns>
    private static string GetBucketName(IConfiguration configuration, ILogger<TLogger> logger)
    {
        var bucketName = Environment.GetEnvironmentVariable("IDRIVE_BUCKET")
                         ?? configuration["IDrive:Bucket"]
                         ?? "rtub";

        return bucketName;
    }

    /// <summary>
    /// Disposes the S3 client
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the S3 client
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _s3Client?.Dispose();
            }
            _disposed = true;
        }
    }
}
