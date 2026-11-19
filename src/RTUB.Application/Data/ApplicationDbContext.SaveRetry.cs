using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Data;

// SQLite lock retry used by SaveChangesAsync.

public partial class ApplicationDbContext
{
    /// <summary>
    /// Maximum number of retry attempts for SQLite "database table is locked" errors.
    /// </summary>
    private const int SqliteRetryCount = 3;

    /// <summary>
    /// Wraps base.SaveChangesAsync with automatic retry for SQLite "database table is locked"
    /// (Error 6). This transient error occurs when multiple connections contend for write access.
    /// </summary>
    private async Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < SqliteRetryCount && IsSqliteTableLocked(ex))
            {
                // Exponential backoff: 50ms, 150ms, ...
                await Task.Delay(50 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Checks whether a DbUpdateException was caused by SQLite Error 6 ("database table is locked").
    /// </summary>
    private static bool IsSqliteTableLocked(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException { SqliteErrorCode: 6 };
    }
}
