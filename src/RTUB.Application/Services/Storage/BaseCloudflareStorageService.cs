using Amazon.S3;
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
}
