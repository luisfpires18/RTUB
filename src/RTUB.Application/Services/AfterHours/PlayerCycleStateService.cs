using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;

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
            await using var context = await contextFactory.CreateDbContextAsync();
            state = PlayerCycleState.CreateInitial(cycle.Id, userId, now);
            context.AfterHoursPlayerCycleStates.Add(state);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (GameCycleService.IsUniqueViolation(ex))
            {
                // Another tab or device created it between our read and our insert: the unique
                // (cycle, user) index kept it to one row, and that row is the answer.
                state = await FindAsync(cycle.Id, userId)
                    ?? throw new InvalidOperationException("Player state vanished after a unique conflict.", ex);
            }
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
            .SingleOrDefaultAsync(s => s.GameCycleId == cycleId && s.UserId == userId);
    }
}
