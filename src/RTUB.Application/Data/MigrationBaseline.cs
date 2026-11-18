using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RTUB.Application.Data;

/// <summary>
/// The 180 migrations up to <see cref="LastSquashedId"/> were squashed into one <see cref="BaselineId"/> migration
/// (task 130). A new database runs the Baseline. A database that already ran the old chain has that schema, so it
/// only gets the Baseline's history row and its old rows stay (an older release started against it still finds
/// every migration it knows). A database that stopped earlier in the old chain cannot be brought forward any more.
/// </summary>
public static class MigrationBaseline
{
    public const string BaselineId = "20261118134743_Baseline";

    /// <summary>The newest migration of the squashed chain. PROD (2.3.12) and DEV ran it before the squash.</summary>
    public const string LastSquashedId = "20261117191014_AddAprenderViewsAndStars";

    /// <summary>Records the Baseline as applied on a database that ran the old chain. Call before migrating.</summary>
    public static async Task AdoptAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        var history = database.GetService<IHistoryRepository>();
        if (!await history.ExistsAsync(cancellationToken))
        {
            return;
        }

        var applied = (await history.GetAppliedMigrationsAsync(cancellationToken)).Select(r => r.MigrationId).ToHashSet();
        if (applied.Count == 0 || applied.Contains(BaselineId))
        {
            return;
        }

        if (!applied.Contains(LastSquashedId))
        {
            throw new InvalidOperationException(
                $"This database stopped at migration '{applied.Max()}', before '{LastSquashedId}'. The migrations up to that one " +
                $"were squashed into '{BaselineId}', so this release cannot bring it forward: start release 2.3.13 or older " +
                "against it once, or restore a newer backup.");
        }

        await database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(BaselineId, ProductInfo.GetVersion())), cancellationToken);
    }
}
