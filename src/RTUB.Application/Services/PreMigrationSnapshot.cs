using Microsoft.Data.Sqlite;

namespace RTUB.Application.Services;

/// <summary>
/// Restore point taken at startup immediately before EF Core migrates an existing database.
///
/// The daily R2 backup can be a day old and keeps only two generations, so by the time a bad
/// migration is noticed it may hold no copy of the pre-release schema at all. This copy is taken
/// by the only process that can take it consistently - the app itself, before it serves a
/// request or starts a background service - through SQLite's online backup API, never a file copy
/// of a live WAL database.
///
/// Fails closed: if the snapshot cannot be created and validated, this throws, startup aborts and
/// nothing is migrated - so the previous release still matches the schema and can be redeployed.
/// Restoring from a snapshot is a manual procedure (docs/release-and-rollback.md); nothing here
/// ever rolls a database back.
/// </summary>
public static class PreMigrationSnapshot
{
    /// <summary>How many snapshots are kept; older ones are deleted after a successful snapshot.</summary>
    public const int Keep = 5;

    // Same threshold the daily backup uses to reject a truncated copy that still passes quick_check.
    private const double MinSizeRatio = 0.5;

    /// <summary>
    /// Snapshots the database behind <paramref name="connectionString"/> into
    /// <c>&lt;database directory&gt;/backups/pre-migration/</c> and returns the snapshot's path.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string the app uses</param>
    /// <param name="targetMigration">The last pending migration, recorded in the file name</param>
    /// <param name="utcNow">Current UTC time, recorded in the file name</param>
    public static string Take(string? connectionString, string targetMigration, DateTime utcNow)
    {
        var resolution = DatabaseBackupBackgroundService.ResolveDatabasePath(connectionString);
        if (resolution.Path == null)
        {
            throw new InvalidOperationException(
                $"Pre-migration snapshot impossible: {resolution.SkipReason}. Migrations were not applied.");
        }

        var directory = Path.Combine(Path.GetDirectoryName(resolution.Path)!, "backups", "pre-migration");
        Directory.CreateDirectory(directory);

        // Only this process writes here, and only at startup: anything half-written is a leftover.
        foreach (var leftover in Directory.GetFiles(directory, "*.partial"))
        {
            File.Delete(leftover);
        }

        var snapshotPath = Path.Combine(directory, $"{utcNow:yyyyMMdd'T'HHmmss'Z'}-before-{targetMigration}.db");
        var partialPath = snapshotPath + ".partial";

        try
        {
            var liveSize = new FileInfo(resolution.Path).Length;
            DatabaseBackupBackgroundService.CreateSnapshot(resolution.Path, partialPath);

            // The copy inherits the live database's WAL flag, so anything that opened it later
            // would grow -wal/-shm sidecars beside it. A restore point must be one file.
            using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = partialPath, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA journal_mode=DELETE;";
                command.ExecuteScalar();
            }

            var validation = DatabaseBackupBackgroundService.ValidateSnapshot(partialPath, liveSize, MinSizeRatio);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Pre-migration snapshot failed validation ({validation.FailureReason}). Migrations were not applied.");
            }

            // Pooled connections keep the file open, and a file that is still open cannot be
            // renamed on Windows.
            SqliteConnection.ClearAllPools();
            File.Move(partialPath, snapshotPath);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }

            throw;
        }

        // Timestamp-first names sort chronologically.
        foreach (var old in Directory.GetFiles(directory, "*.db").OrderDescending(StringComparer.Ordinal).Skip(Keep))
        {
            File.Delete(old);
        }

        return snapshotPath;
    }
}
