using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RTUB.Application.Data;

namespace RTUB.Application.Services.AfterHours;

/// <summary>
/// The one way After Hours writes. Runs <paramref name="work"/> in a fresh context inside a SQLite
/// write transaction (<c>BEGIN IMMEDIATE</c>, Microsoft.Data.Sqlite's default), so concurrent writers
/// are serialized and each sees the previous one's result. The work commits explicitly; anything it
/// does not commit is rolled back. Lock contention (SQLITE_BUSY past busy_timeout, SQLITE_LOCKED under
/// shared cache) or a lost unique race rolls back and runs the whole work again.
/// </summary>
internal static class AfterHoursWriteTransaction
{
    private const int MaxAttempts = 40;

    public static async Task<T> RunAsync<T>(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        Func<ApplicationDbContext, IDbContextTransaction, Task<T>> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();
                return await work(context, transaction);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsRetryable(ex))
            {
                // Nothing was committed: start over and see whoever won.
                await Task.Delay(Random.Shared.Next(5, 25));
            }
        }
    }

    private static bool IsRetryable(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is SqliteException { SqliteErrorCode: 5 or 6 } or SqliteException { SqliteExtendedErrorCode: 2067 })
                return true;
        }
        return false;
    }
}
