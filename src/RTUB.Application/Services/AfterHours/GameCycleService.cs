using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services.AfterHours;

public class GameCycleService(IDbContextFactory<ApplicationDbContext> contextFactory, TimeProvider clock) : IGameCycleService
{
    public async Task<GameCycle?> GetActiveCycleAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        // The filtered unique index guarantees at most one Active row.
        var cycle = await context.AfterHoursGameCycles
            .AsNoTracking()
            .Include(c => c.FiscalYear)
            .SingleOrDefaultAsync(c => c.Status == GameCycleStatus.Active);

        return cycle is not null && cycle.IsPlayableAt(clock.GetUtcNow().UtcDateTime) ? cycle : null;
    }

    public async Task<GameCycle> CreateCycleAsync(int fiscalYearId, GameCycleKind kind, DateTime startUtc, DateTime endUtc)
    {
        var cycle = GameCycle.Create(fiscalYearId, kind, startUtc, endUtc);

        await using var context = await contextFactory.CreateDbContextAsync();
        context.AfterHoursGameCycles.Add(cycle);
        await context.SaveChangesAsync();
        return cycle;
    }

    public async Task ActivateAsync(int cycleId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var cycle = await FindAsync(context, cycleId);

        if (cycle.Status != GameCycleStatus.Scheduled)
            throw new InvalidOperationException($"Cycle {cycleId} is {cycle.Status}; only a Scheduled cycle can be activated.");

        cycle.Status = GameCycleStatus.Active;
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("Another After Hours cycle is already active.", ex);
        }
    }

    private static async Task<GameCycle> FindAsync(ApplicationDbContext context, int cycleId) =>
        await context.AfterHoursGameCycles.FindAsync(cycleId)
            ?? throw new EntityNotFoundException(nameof(GameCycle), cycleId);

    /// <summary>SQLITE_CONSTRAINT_UNIQUE (2067).</summary>
    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 };
}
