using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Outcome of validating a snapshot file before it is allowed near the stored backups.
/// </summary>
/// <param name="IsValid">Whether the snapshot passed every check</param>
/// <param name="FailureReason">Why it failed, or null when valid</param>
public sealed record SnapshotValidation(bool IsValid, string? FailureReason)
{
    public static SnapshotValidation Ok() => new(true, null);

    public static SnapshotValidation Fail(string reason) => new(false, reason);
}

/// <summary>
/// Result of working out which database file to back up.
/// </summary>
/// <param name="Path">Full path of the live database, or null when there is nothing to back up</param>
/// <param name="SkipReason">Why the backup is being skipped, or null when a path was resolved</param>
public sealed record DatabasePathResolution(string? Path, string? SkipReason)
{
    public static DatabasePathResolution Resolved(string path) => new(path, null);

    public static DatabasePathResolution Skip(string reason) => new(null, reason);
}

/// <summary>
/// Background service that takes a daily online snapshot of the live SQLite database and
/// uploads it to a private Cloudflare R2 bucket, keeping exactly two generations:
/// current and previous.
/// </summary>
/// <remarks>
/// The database runs in WAL mode, so copying app.db / app.db-wal / app.db-shm off disk is
/// not a valid backup - the copy can land mid-transaction. This uses SQLite's online
/// backup API (<see cref="SqliteConnection.BackupDatabase(SqliteConnection)"/>), which
/// produces a consistent snapshot without blocking writers.
///
/// A snapshot is uploaded to a staging key and only promoted to current.db once it has
/// passed integrity checks and its uploaded size has been verified, so a failed or corrupt
/// run can never replace the last known-good backup.
/// </remarks>
public class DatabaseBackupBackgroundService : BackgroundService
{
    private readonly ILogger<DatabaseBackupBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly DatabaseBackupOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 15;

    /// <summary>
    /// Fallback run time used when the configured value cannot be parsed.
    /// </summary>
    public static readonly TimeSpan DefaultScheduledTime = new(3, 30, 0);

    public DatabaseBackupBackgroundService(
        ILogger<DatabaseBackupBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        IOptions<DatabaseBackupOptions> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        if (!TimeSpan.TryParse(_options.ScheduledTime, out _))
        {
            _logger.LogWarning(
                "Invalid database backup scheduled time '{ScheduledTime}'. Falling back to {Default}",
                _options.ScheduledTime, DefaultScheduledTime);
        }

        _logger.LogInformation(
            "Database backup service started. Daily snapshot at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime(_options.ScheduledTime, DateTime.UtcNow);
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next database backup scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                if (_options.Enabled)
                {
                    await RunBackupAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a failed backup take the host down; the previous backup stands.
                _logger.LogError(ex, "Error in database backup service");
            }
        }
    }

    private async Task RunBackupAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        if (_lastRunDate == today)
        {
            _logger.LogDebug("Database backup already ran today, skipping");
            return;
        }

        // Spread instances apart when the App Service is scaled out, so two of them do not
        // rotate the backup generations at the same moment.
        if (_options.MaxJitterSeconds > 0)
        {
            var jitter = Random.Shared.Next(0, _options.MaxJitterSeconds + 1);
            _logger.LogDebug("Delaying database backup by {Jitter}s of jitter", jitter);
            await Task.Delay(TimeSpan.FromSeconds(jitter), cancellationToken);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var backupStorage = scope.ServiceProvider.GetRequiredService<IDatabaseBackupStorageService>();

        // If another instance already produced today's backup, do nothing. Combined with the
        // jitter above this makes a concurrent rotation very unlikely on a scaled-out plan.
        var currentTimestamp = await backupStorage.GetLastModifiedUtcAsync(_options.CurrentKey);
        if (currentTimestamp?.Date == today)
        {
            _logger.LogInformation("Database backup for {Date:yyyy-MM-dd} already exists, skipping", today);
            _lastRunDate = today;
            return;
        }

        var resolution = ResolveDatabasePath(_configuration.GetConnectionString("SqliteConnection"));
        if (resolution.Path == null)
        {
            _logger.LogWarning("Skipping database backup: {Reason}", resolution.SkipReason);
            return;
        }

        var sourcePath = resolution.Path;
        var liveSize = new FileInfo(sourcePath).Length;
        var tempPath = Path.Combine(Path.GetTempPath(), $"rtub-backup-{Guid.NewGuid():N}.db");

        try
        {
            CreateSnapshot(sourcePath, tempPath);

            var validation = ValidateSnapshot(tempPath, liveSize, _options.MinSizeRatio);
            if (!validation.IsValid)
            {
                // Nothing has been uploaded at this point, so the stored backups are untouched.
                _logger.LogError(
                    "Database snapshot failed validation ({Reason}). Existing backups left untouched.",
                    validation.FailureReason);
                return;
            }

            var snapshotSize = new FileInfo(tempPath).Length;

            await backupStorage.UploadFileAsync(_options.StagingKey, tempPath, cancellationToken);

            var uploadedSize = await backupStorage.GetSizeAsync(_options.StagingKey);
            if (uploadedSize != snapshotSize)
            {
                _logger.LogError(
                    "Uploaded backup size mismatch (local {LocalSize} bytes, remote {RemoteSize} bytes). Rotation aborted; existing backups left untouched.",
                    snapshotSize, uploadedSize);
                return;
            }

            await RotateAsync(backupStorage, cancellationToken);

            _lastRunDate = today;
            _logger.LogInformation(
                "Database backup completed. Snapshot {SnapshotSize:N0} bytes from a {LiveSize:N0} byte database promoted to {CurrentKey}",
                snapshotSize, liveSize, _options.CurrentKey);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    /// <summary>
    /// Promotes the verified staging object to current.db, demoting the old current.db to
    /// previous.db first. Both steps are server-side copies, so nothing is re-uploaded.
    /// </summary>
    public async Task RotateAsync(IDatabaseBackupStorageService backupStorage, CancellationToken cancellationToken = default)
    {
        if (await backupStorage.ExistsAsync(_options.CurrentKey))
        {
            await backupStorage.CopyAsync(_options.CurrentKey, _options.PreviousKey, cancellationToken);
            _logger.LogDebug("Demoted {CurrentKey} to {PreviousKey}", _options.CurrentKey, _options.PreviousKey);
        }
        else
        {
            _logger.LogInformation("No existing {CurrentKey}; this is the first backup", _options.CurrentKey);
        }

        await backupStorage.CopyAsync(_options.StagingKey, _options.CurrentKey, cancellationToken);

        try
        {
            await backupStorage.DeleteAsync(_options.StagingKey, cancellationToken);
        }
        catch (Exception ex)
        {
            // The backup itself succeeded; a leftover staging object is harmless and gets
            // overwritten by the next run.
            _logger.LogWarning(ex, "Failed to delete staging object {StagingKey} after a successful rotation", _options.StagingKey);
        }
    }

    /// <summary>
    /// Takes a consistent snapshot of the live database using SQLite's online backup API.
    /// Safe to run against a database in WAL mode while it is being written to.
    /// </summary>
    /// <param name="sourcePath">Path of the live database file</param>
    /// <param name="tempPath">Path the snapshot is written to</param>
    public static void CreateSnapshot(string sourcePath, string tempPath)
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 30
        }.ToString();

        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = tempPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var source = new SqliteConnection(sourceConnectionString);
        using var destination = new SqliteConnection(destinationConnectionString);

        source.Open();
        destination.Open();

        source.BackupDatabase(destination);
    }

    /// <summary>
    /// Verifies a snapshot is a readable, complete SQLite database.
    /// </summary>
    /// <param name="tempPath">Path of the snapshot to check</param>
    /// <param name="liveSize">Size of the live database, used to detect a truncated snapshot</param>
    /// <param name="minSizeRatio">Minimum snapshot size as a fraction of the live database</param>
    public static SnapshotValidation ValidateSnapshot(string tempPath, long liveSize, double minSizeRatio)
    {
        if (!File.Exists(tempPath))
        {
            return SnapshotValidation.Fail($"snapshot file was not created at {tempPath}");
        }

        var snapshotSize = new FileInfo(tempPath).Length;
        if (snapshotSize == 0)
        {
            return SnapshotValidation.Fail("snapshot is empty");
        }

        // A snapshot much smaller than the live database means a truncated copy, which can
        // still be internally consistent and therefore still pass quick_check.
        if (liveSize > 0 && snapshotSize < liveSize * minSizeRatio)
        {
            return SnapshotValidation.Fail(
                $"snapshot is {snapshotSize:N0} bytes against a {liveSize:N0} byte live database, below the {minSizeRatio:P0} threshold");
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = tempPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA quick_check;";
                var result = command.ExecuteScalar() as string;

                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return SnapshotValidation.Fail($"PRAGMA quick_check returned '{result ?? "(null)"}'");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
                var objectCount = Convert.ToInt64(command.ExecuteScalar());

                if (objectCount == 0)
                {
                    return SnapshotValidation.Fail("snapshot contains no tables");
                }
            }

            return SnapshotValidation.Ok();
        }
        catch (Exception ex)
        {
            return SnapshotValidation.Fail($"snapshot could not be opened: {ex.Message}");
        }
    }

    /// <summary>
    /// Works out which file to back up from the configured connection string.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string, or null to use the default</param>
    public static DatabasePathResolution ResolveDatabasePath(string? connectionString)
    {
        connectionString ??= "Data Source=app.db";

        string? dataSource;
        try
        {
            dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        }
        catch (Exception ex)
        {
            return DatabasePathResolution.Skip($"the connection string could not be parsed ({ex.Message})");
        }

        if (string.IsNullOrEmpty(dataSource))
        {
            return DatabasePathResolution.Skip("the connection string has no data source");
        }

        if (dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return DatabasePathResolution.Skip("the database is in-memory");
        }

        var fullPath = Path.GetFullPath(dataSource);

        if (!File.Exists(fullPath))
        {
            return DatabasePathResolution.Skip($"no database file at {fullPath}");
        }

        return DatabasePathResolution.Resolved(fullPath);
    }

    /// <summary>
    /// Next UTC run time for the given "HH:mm" schedule, rolling to tomorrow when today's
    /// slot has already passed.
    /// </summary>
    public static DateTime CalculateNextRunTime(string? scheduledTime, DateTime nowUtc)
    {
        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            timeOfDay = DefaultScheduledTime;
        }

        var nextRun = nowUtc.Date.Add(timeOfDay);

        return nextRun <= nowUtc ? nextRun.AddDays(1) : nextRun;
    }

    private void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                _logger.LogDebug("Deleted temporary snapshot {TempPath}", tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary snapshot {TempPath}", tempPath);
        }
    }
}
