namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the daily SQLite database backup to Cloudflare R2.
/// </summary>
public class DatabaseBackupOptions
{
    /// <summary>
    /// The configuration section name
    /// </summary>
    public const string SectionName = "DatabaseBackup";

    /// <summary>
    /// Whether the daily backup runs. Disabled by default so non-production
    /// environments never write to the backup bucket.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Daily run time in UTC, "HH:mm" format.
    /// </summary>
    public string ScheduledTime { get; set; } = "03:30";

    /// <summary>
    /// Private R2 bucket that holds the backups. Required when <see cref="Enabled"/> is true.
    /// </summary>
    public string? Bucket { get; set; }

    /// <summary>
    /// Cloudflare account ID for the backup bucket.
    /// Falls back to Cloudflare:R2:AccountId when not set.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Access key for the backup bucket. Falls back to Cloudflare:R2:AccessKeyId when not set.
    /// Prefer a token scoped to the backup bucket only.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// Secret key for the backup bucket. Falls back to Cloudflare:R2:SecretAccessKey when not set.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Object key of the most recent known-good backup.
    /// </summary>
    public string CurrentKey { get; set; } = "database/current.db";

    /// <summary>
    /// Object key of the previous known-good backup.
    /// </summary>
    public string PreviousKey { get; set; } = "database/previous.db";

    /// <summary>
    /// Object key the fresh snapshot is uploaded to before rotation. A snapshot only
    /// becomes <see cref="CurrentKey"/> after it has been fully uploaded and verified,
    /// so a failed or corrupt run can never replace the last known-good backup.
    /// </summary>
    public string StagingKey { get; set; } = "database/incoming.db";

    /// <summary>
    /// Maximum random delay before a run, in seconds. Spreads out instances when the
    /// App Service is scaled out so they do not rotate concurrently.
    /// </summary>
    public int MaxJitterSeconds { get; set; } = 120;

    /// <summary>
    /// A snapshot smaller than this fraction of the live database is rejected as
    /// truncated. Guards against a half-written snapshot that still passes quick_check.
    /// </summary>
    public double MinSizeRatio { get; set; } = 0.5;
}
