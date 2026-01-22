using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically backs up the SQLite database at configured times.
/// Runs at configured times (default: 08:00, 20:00) to create a backup copy of the database.
/// </summary>
public class DatabaseBackupBackgroundService : BackgroundService
{
    private readonly ILogger<DatabaseBackupBackgroundService> _logger;
    private readonly DatabaseBackupOptions _options;

    private const int StartupDelaySeconds = 20;

    public DatabaseBackupBackgroundService(
        ILogger<DatabaseBackupBackgroundService> logger,
        IOptions<DatabaseBackupOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit before starting to allow the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Database backup service started. Backup times: {BackupTimes}. Enabled: {Enabled}",
            string.Join(", ", _options.BackupTimes),
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
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
                    await PerformBackupAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in database backup service");
            }
        }
    }

    private async Task PerformBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sourcePath = _options.SourceDatabasePath;
            var backupPath = _options.BackupDatabasePath;

            // Validate source database exists
            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning(
                    "Source database file not found at {SourcePath}. Skipping backup.",
                    sourcePath);
                return;
            }

            // Create backup directory if it doesn't exist
            var backupDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDirectory) && !Directory.Exists(backupDirectory))
            {
                try
                {
                    Directory.CreateDirectory(backupDirectory);
                    _logger.LogInformation("Created backup directory at {BackupDirectory}", backupDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create backup directory at {BackupDirectory}", backupDirectory);
                    return;
                }
            }

            // Get file size for logging
            var fileInfo = new FileInfo(sourcePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

            _logger.LogInformation(
                "Starting database backup from {SourcePath} to {BackupPath} (Size: {SizeMB:F2} MB)",
                sourcePath,
                backupPath,
                fileSizeMB);

            // Perform the backup by copying the file
            // Note: For SQLite, this is a simple file copy. For production environments with high concurrency,
            // consider using SQLite's backup API (sqlite3_backup_*) for transactional consistency.
            // Use await with Task.Run to avoid blocking the thread for large files
            await Task.Run(() =>
            {
                File.Copy(sourcePath, backupPath, overwrite: true);
            }, cancellationToken);

            _logger.LogInformation(
                "Database backup completed successfully. Backup saved to {BackupPath}",
                backupPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Database backup operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform database backup");
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var backupTimes = _options.BackupTimes
            .Select(t => TimeSpan.TryParse(t, out var ts) ? ts : (TimeSpan?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .OrderBy(t => t)
            .ToList();

        if (!backupTimes.Any())
        {
            _logger.LogWarning("No valid backup times configured. Using default 08:00");
            backupTimes.Add(new TimeSpan(8, 0, 0));
        }

        // Find the next backup time today or tomorrow
        foreach (var time in backupTimes)
        {
            var nextRun = now.Date.Add(time);
            if (nextRun > now)
            {
                return nextRun;
            }
        }

        // All times have passed today, schedule for tomorrow's first time
        return now.Date.AddDays(1).Add(backupTimes.First());
    }
}
