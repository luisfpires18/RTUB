using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;
using RTUB.Application.Services.Storage;
using RTUB.Core.Helpers;
using RTUB.Web.Services;

namespace RTUB.Web.Extensions;

// Email, object storage (R2) and database backup registrations.

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers email notification services
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        // Email notification dependencies (following SRP)
        services.AddScoped<EmailConfigurationProvider>();
        services.AddScoped<SmtpClientFactory>();
        services.AddScoped<EmailRateLimiter>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        return services;
    }

    /// <summary>
    /// Guards against a non-production environment being pointed at the production bucket.
    /// </summary>
    /// <remarks>
    /// <c>appsettings.json</c> commits <c>Cloudflare:R2:Bucket</c> as the production bucket name,
    /// so an environment that forgets to override it inherits production's bucket and every
    /// upload and delete lands there. No per-object ownership check can catch that - the bucket
    /// really is the one configured - so it is refused at startup instead, before any service can
    /// be resolved. Production itself is never checked and is unaffected.
    /// </remarks>
    private static void GuardAgainstProductionBucket(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            return;
        }

        var bucket = configuration["Cloudflare:R2:Bucket"];
        var productionBucket = configuration["Cloudflare:R2:ProductionBucket"];

        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(productionBucket))
        {
            return;
        }

        // Only an environment that can actually reach R2 is worth refusing. With no credential
        // the S3 client cannot touch any bucket, production's included, so there is nothing to
        // guard - which is what lets the integration-test host run on the committed settings.
        //
        // Deliberately a capability check, not an allow-list of environment names: exempting
        // "Test" by name would exempt anything that called itself Test, credentials and all.
        if (string.IsNullOrWhiteSpace(configuration["Cloudflare:R2:AccessKeyId"]) ||
            string.IsNullOrWhiteSpace(configuration["Cloudflare:R2:SecretAccessKey"]))
        {
            return;
        }

        if (string.Equals(bucket.Trim(), productionBucket.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Environment '{environment.EnvironmentName}' is configured with Cloudflare:R2:Bucket = "
                + "the production bucket. A non-production environment must have its own bucket: every "
                + "upload and every delete it performs would otherwise be applied to production data. "
                + "Set Cloudflare__R2__Bucket to this environment's own bucket.");
        }
    }

    /// <summary>
    /// Registers storage services (images, audio, lyrics, documents)
    /// </summary>
    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        GuardAgainstProductionBucket(configuration, environment);

        // Read-only view of the production bucket, for a non-production app running on a
        // sanitized production snapshot. Always registered, but inert unless the environment is
        // non-production AND a dedicated Cloudflare:R2:Reference:* credential is configured - so
        // in production it holds no client and answers "not found" to everything.
        services.AddSingleton<IReferenceStorageService, ReferenceStorageService>();

        services.AddScoped<IImageStorageService, CloudflareImageStorageService>();
        services.AddScoped<IAudioStorageService, CloudflareAudioStorageService>();
        services.AddScoped<ILyricStorageService, CloudflareLyricStorageService>();
        services.AddScoped<IDocumentStorageService, CloudflareDocumentStorageService>();
        services.AddScoped<IEventMediaStorageService, CloudflareEventMediaStorageService>();
        services.AddScoped<ISongVideoStorageService, CloudflareSongVideoStorageService>();
        services.AddScoped<IEventVideoStorageService, CloudflareEventVideoStorageService>();
        services.AddScoped<IGalleryMediaStorageService, CloudflareGalleryMediaStorageService>();
        services.AddScoped<IReceiptStorageService, CloudflareReceiptStorageService>();
        services.AddScoped<INaipeMediaStorageService, CloudflareNaipeMediaStorageService>();

        return services;
    }

    /// <summary>
    /// Key for the S3 client that talks to the private database-backup bucket.
    /// Kept separate from the media client so the backup can use a bucket-scoped token.
    /// </summary>
    private const string BackupS3ClientKey = "R2Backup";

    /// <summary>
    /// Registers the daily SQLite backup: its own R2 client, storage service and scheduler.
    /// Registers nothing when DatabaseBackup:Enabled is false, so non-production
    /// environments never touch the backup bucket.
    /// </summary>
    public static IServiceCollection AddDatabaseBackupServices(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(RTUB.Application.Configuration.DatabaseBackupOptions.SectionName)
            .Get<RTUB.Application.Configuration.DatabaseBackupOptions>();

        if (options?.Enabled != true)
        {
            return services;
        }

        // Credentials fall back to the media R2 settings when a dedicated backup token is
        // not configured. Resolved lazily so a misconfiguration surfaces as a clear error
        // in the logs rather than at container build time.
        services.AddKeyedSingleton<Amazon.S3.IAmazonS3>(BackupS3ClientKey, (serviceProvider, _) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var accessKey = configuration["DatabaseBackup:AccessKeyId"] ?? configuration["Cloudflare:R2:AccessKeyId"];
            var secretKey = configuration["DatabaseBackup:SecretAccessKey"] ?? configuration["Cloudflare:R2:SecretAccessKey"];
            var accountId = configuration["DatabaseBackup:AccountId"] ?? configuration["Cloudflare:R2:AccountId"];

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                var errorMsg = "Database backup credentials not configured. Set DatabaseBackup:AccessKeyId and DatabaseBackup:SecretAccessKey (or fall back to the Cloudflare:R2 ones).";
                logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            if (string.IsNullOrEmpty(accountId))
            {
                var errorMsg = "Database backup account ID not configured. Set DatabaseBackup:AccountId or Cloudflare:R2:AccountId.";
                logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
            var config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                AuthenticationRegion = "auto"
            };
            return new Amazon.S3.AmazonS3Client(credentials, config);
        });

        services.AddScoped<IDatabaseBackupStorageService>(serviceProvider =>
            new DatabaseBackupStorageService(
                serviceProvider.GetRequiredKeyedService<Amazon.S3.IAmazonS3>(BackupS3ClientKey),
                serviceProvider.GetRequiredService<IOptions<RTUB.Application.Configuration.DatabaseBackupOptions>>(),
                serviceProvider.GetRequiredService<ILogger<DatabaseBackupStorageService>>()));

        services.AddHostedService<DatabaseBackupBackgroundService>();

        return services;
    }
}
