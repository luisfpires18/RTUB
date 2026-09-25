using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

/// <summary>
/// Lazy, persisted rotation: the first request in a window creates its contracts inside a write
/// transaction; every later request (any tab, any player) reads the same rows. No background job.
/// </summary>
public class BuyerContractService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider clock) : IBuyerContractService
{
    public async Task<IReadOnlyList<BuyerContractView>> GetCurrentContractsAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = clock.GetUtcNow().UtcDateTime;
        var windowStart = BuyerContractRules.WindowStart(now);

        await using var context = await contextFactory.CreateDbContextAsync();
        var cycle = await context.AfterHoursGameCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Status == GameCycleStatus.Active);
        if (cycle is null || !cycle.IsPlayableAt(now))
            return [];

        var contracts = await LoadWindowAsync(context, cycle.Id, windowStart);
        if (contracts.Count < BuyerContractRules.ContractsPerRotation)
            contracts = await CreateWindowAsync(cycle.Id, windowStart);

        var ids = contracts.Select(c => c.Id).ToList();
        var completed = await context.AfterHoursBuyerContractCompletions.AsNoTracking()
            .Where(c => ids.Contains(c.BuyerContractId))
            .Join(context.AfterHoursPlayerCycleStates.Where(s => s.UserId == userId),
                c => c.PlayerCycleStateId, s => s.Id, (c, _) => c.BuyerContractId)
            .ToListAsync();

        return contracts.Select(c => new BuyerContractView(c, completed.Contains(c.Id))).ToList();
    }

    private Task<List<BuyerContract>> CreateWindowAsync(int cycleId, DateTime windowStart) =>
        AfterHoursWriteTransaction.RunAsync(contextFactory, async (context, transaction) =>
        {
            // Never rotate into a cycle a rollover finished meanwhile: that cycle is history.
            if (!await context.AfterHoursGameCycles.AnyAsync(c => c.Id == cycleId && c.Status == GameCycleStatus.Active))
                return [];

            // Re-read under the write lock: another request may have created them meanwhile.
            var existing = await LoadWindowAsync(context, cycleId, windowStart);
            var missing = Enumerable.Range(0, BuyerContractRules.ContractsPerRotation)
                .Except(existing.Select(c => c.Slot));
            foreach (var slot in missing)
                context.AfterHoursBuyerContracts.Add(BuyerContractRules.Create(cycleId, windowStart, slot));

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await LoadWindowAsync(context, cycleId, windowStart);
        });

    private static Task<List<BuyerContract>> LoadWindowAsync(ApplicationDbContext context, int cycleId, DateTime windowStart) =>
        context.AfterHoursBuyerContracts.AsNoTracking()
            .Where(c => c.GameCycleId == cycleId && c.RotationStartUtc == windowStart)
            .OrderBy(c => c.Slot)
            .ToListAsync();
}
