// Database backup: snapshot, validation, path resolution and schedule helpers.
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services;

public partial class DatabaseBackupBackgroundService
{
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