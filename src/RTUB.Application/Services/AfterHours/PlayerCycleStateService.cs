using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public class PlayerCycleStateService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IGameCycleService cycleService) : IPlayerCycleStateService
{
    public async Task<PlayerCycleState?> GetOrCreateForActiveCycleAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var cycle = await cycleService.GetActiveCycleAsync();
        if (cycle is null)
            return null;

        var existing = await FindAsync(cycle.Id, userId);
        if (existing is not null)
            return existing;

        await using var context = await contextFactory.CreateDbContextAsync();
        var state = PlayerCycleState.CreateInitial(cycle.Id, userId, DateTime.UtcNow);
        context.AfterHoursPlayerCycleStates.Add(state);
        try
        {
            await context.SaveChangesAsync();
            return state;
        }
        catch (DbUpdateException ex) when (GameCycleService.IsUniqueViolation(ex))
        {
            // Another tab or device created it between our read and our insert: the unique
            // (cycle, user) index kept it to one row, and that row is the answer.
            return await FindAsync(cycle.Id, userId)
                ?? throw new InvalidOperationException("Player state vanished after a unique conflict.", ex);
        }
    }

    private async Task<PlayerCycleState?> FindAsync(int cycleId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.AfterHoursPlayerCycleStates
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.GameCycleId == cycleId && s.UserId == userId);
    }
}
