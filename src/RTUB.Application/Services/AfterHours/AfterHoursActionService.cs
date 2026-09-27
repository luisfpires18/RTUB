using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public sealed class AfterHoursDice : IAfterHoursDice
{
    public int RollPercent() => Random.Shared.Next(1, 101);
}

/// <summary>
/// Runs each After Hours action as one SQLite write transaction (<c>BEGIN IMMEDIATE</c>, which
/// Microsoft.Data.Sqlite issues for a default transaction): receipt lookup, state load,
/// reconciliation, rules, dice, state update and receipt insert all happen under the database
/// write lock, so concurrent tabs are serialized and each sees the previous one's result.
/// Nothing is written for a refusal.
/// </summary>
public class AfterHoursActionService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider clock,
    IAfterHoursDice dice) : IAfterHoursActionService
{
    public const int MaxIdempotencyKeyLength = 64;
    private const int MaxLockAttempts = 40;

    public Task<AfterHoursActionResult> CommitCrimeAsync(string userId, string crimeId, CrimeApproach approach, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.CrimeRequest(crimeId, approach),
            (state, now) => AfterHoursActions.CommitCrime(state, crimeId, approach, now, dice.RollPercent));

    public Task<AfterHoursActionResult> TakeCoverJobAsync(string userId, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.CoverJobRequest,
            (state, now) => AfterHoursActions.TakeCoverJob(state, now));

    public Task<AfterHoursActionResult> DepositAsync(string userId, long amount, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.DepositRequest(amount),
            (state, _) => AfterHoursActions.Deposit(state, amount));

    public Task<AfterHoursActionResult> WithdrawAsync(string userId, long amount, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.WithdrawRequest(amount),
            (state, _) => AfterHoursActions.Withdraw(state, amount));

    private async Task<AfterHoursActionResult> RunAsync(
        string userId, string idempotencyKey, string request, Func<PlayerCycleState, DateTime, ActionAttempt> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
            return new AfterHoursActionResult(null, null, "Invalid request key.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RunOnceAsync(userId, idempotencyKey, request, action);
            }
            catch (Exception ex) when (attempt < MaxLockAttempts && IsRetryable(ex))
            {
                // Another action holds the write lock (SQLITE_BUSY past busy_timeout, or
                // SQLITE_LOCKED under shared cache), or won a unique race on the same key or
                // first-time state. Nothing was committed: start over and see its result.
                await Task.Delay(Random.Shared.Next(5, 25));
            }
        }
    }

    private async Task<AfterHoursActionResult> RunOnceAsync(
        string userId, string idempotencyKey, string request, Func<PlayerCycleState, DateTime, ActionAttempt> action)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var now = clock.GetUtcNow().UtcDateTime;

        var prior = await context.AfterHoursPlayerActionReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey && r.PlayerCycleState!.UserId == userId);
        if (prior is not null)
        {
            var priorState = await context.AfterHoursPlayerCycleStates.AsNoTracking()
                .SingleAsync(s => s.Id == prior.PlayerCycleStateId);
            priorState.Reconcile(now);
            return prior.Request == request
                ? new AfterHoursActionResult(prior, priorState, null, Replayed: true)
                : new AfterHoursActionResult(null, priorState, "That request key was already used for a different action.");
        }

        var cycle = await context.AfterHoursGameCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Status == GameCycleStatus.Active);
        if (cycle is null || !cycle.IsPlayableAt(now))
            return new AfterHoursActionResult(null, null, "There is no active After Hours cycle.");

        var state = await context.AfterHoursPlayerCycleStates
            .SingleOrDefaultAsync(s => s.GameCycleId == cycle.Id && s.UserId == userId);
        if (state is null)
        {
            // Safe inside the write transaction: no other action can insert it meanwhile.
            state = PlayerCycleState.CreateInitial(cycle.Id, userId, now);
            context.AfterHoursPlayerCycleStates.Add(state);
            await context.SaveChangesAsync();
        }

        var result = action(state, now);
        if (result.Receipt is null)
        {
            // Refused: roll back, including any reconciliation, and report the reconciled view.
            await transaction.RollbackAsync();
            return new AfterHoursActionResult(null, state, result.Error);
        }

        result.Receipt.PlayerCycleStateId = state.Id;
        result.Receipt.IdempotencyKey = idempotencyKey;
        context.AfterHoursPlayerActionReceipts.Add(result.Receipt);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new AfterHoursActionResult(result.Receipt, state, null);
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
