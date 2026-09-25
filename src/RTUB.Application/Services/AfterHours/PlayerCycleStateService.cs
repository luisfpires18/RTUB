using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public class PlayerCycleStateService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IGameCycleService cycleService,
    TimeProvider clock) : IPlayerCycleStateService
{
    public async Task<PlayerCycleState?> GetOrCreateForActiveCycleAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var cycle = await cycleService.GetActiveCycleAsync();
        if (cycle is null)
            return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var state = await FindAsync(cycle.Id, userId);
        if (state is null)
        {
            // Created under the write lock, re-checking the cycle: a rollover that finished it in the
            // meantime is seen here, so no state is ever added to an archived cycle. A concurrent first
            // visit that won is found instead of inserted twice (the unique index is the backstop).
            state = await AfterHoursWriteTransaction.RunAsync<PlayerCycleState?>(contextFactory, async (context, transaction) =>
            {
                var stillActive = await context.AfterHoursGameCycles
                    .AnyAsync(c => c.Id == cycle.Id && c.Status == GameCycleStatus.Active);
                if (!stillActive)
                    return null;

                var existing = await context.AfterHoursPlayerCycleStates.AsNoTracking()
                    .Include(s => s.Cargo)
                    .Include(s => s.Gear)
                    .SingleOrDefaultAsync(s => s.GameCycleId == cycle.Id && s.UserId == userId);
                if (existing is not null)
                    return existing;

                var created = PlayerCycleState.CreateInitial(cycle.Id, userId, now);
                context.AfterHoursPlayerCycleStates.Add(created);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return created;
            });
            if (state is null)
                return null;
        }

        // A read-only view brought up to now; nothing is written. Actions reconcile the stored row
        // to the same result inside their own transaction.
        state.Reconcile(now);
        return state;
    }

    private async Task<PlayerCycleState?> FindAsync(int cycleId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.AfterHoursPlayerCycleStates
            .AsNoTracking()
            .Include(s => s.Cargo)
            .Include(s => s.Gear)
            .SingleOrDefaultAsync(s => s.GameCycleId == cycleId && s.UserId == userId);
    }
}
