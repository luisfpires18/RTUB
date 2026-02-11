using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RTUB.Application.Data;

/// <summary>
/// SQLite connection interceptor that configures optimal settings for concurrency.
/// Sets WAL journal mode for better read/write concurrency.
/// </summary>
public class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ConfigureConnection(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ConfigureConnection(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void ConfigureConnection(DbConnection connection)
    {
        // Only execute PRAGMA for SQLite connections
        if (connection is not SqliteConnection)
        {
            return;
        }

        try
        {
            // Enable WAL (Write-Ahead Logging) mode for better concurrency.
            // WAL allows readers and writers to operate simultaneously without blocking each other.
            // This setting persists in the database file, but we set it on each connection to ensure it's active.
            using var command = connection.CreateCommand();
            command.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;
                PRAGMA mmap_size = 268435456;
                PRAGMA cache_size = -64000;
            ";
            command.ExecuteNonQuery();
        }
        catch
        {
            // WAL mode is a performance optimization, not critical for correctness.
            // If it fails (e.g., read-only database), continue without it.
        }
    }
}
