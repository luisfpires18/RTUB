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
/// Runs each After Hours action as one write transaction (<see cref="AfterHoursWriteTransaction"/>):
/// receipt lookup, state load, reconciliation, rules, dice, state update and receipt insert all
/// happen under the database write lock. Nothing is written for a refusal.
/// </summary>
public partial class AfterHoursActionService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider clock,
    IAfterHoursDice dice) : IAfterHoursActionService
{
    public const int MaxIdempotencyKeyLength = 64;

    private delegate Task<ActionAttempt> Action(ApplicationDbContext context, PlayerCycleState state, DateTime utcNow);

    public Task<AfterHoursActionResult> CommitCrimeAsync(string userId, string crimeId, CrimeApproach approach, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.CrimeRequest(crimeId, approach),
            (_, state, now) => Task.FromResult(AfterHoursActions.CommitCrime(state, crimeId, approach, now, dice.RollPercent)));

    public Task<AfterHoursActionResult> TakeCoverJobAsync(string userId, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.CoverJobRequest,
            (_, state, now) => Task.FromResult(AfterHoursActions.TakeCoverJob(state, now)));

    public Task<AfterHoursActionResult> DepositAsync(string userId, long amount, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.DepositRequest(amount),
            (_, state, _) => Task.FromResult(AfterHoursActions.Deposit(state, amount)));

    public Task<AfterHoursActionResult> WithdrawAsync(string userId, long amount, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.WithdrawRequest(amount),
            (_, state, _) => Task.FromResult(AfterHoursActions.Withdraw(state, amount)));

    public Task<AfterHoursActionResult> SellToFenceAsync(string userId, CargoType cargo, int quantity, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.FenceRequest(cargo, quantity),
            (_, state, _) => Task.FromResult(AfterHoursActions.SellToFence(state, cargo, quantity)));

    public Task<AfterHoursActionResult> DeliverContractAsync(string userId, int buyerContractId, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.ContractRequest(buyerContractId),
            async (context, state, now) =>
            {
                var contract = await context.AfterHoursBuyerContracts.AsNoTracking()
                    .SingleOrDefaultAsync(c => c.Id == buyerContractId);
                if (contract is null)
                    return ActionAttempt.Reject("Unknown contract.");

                var alreadyCompleted = await context.AfterHoursBuyerContractCompletions
                    .AnyAsync(c => c.BuyerContractId == buyerContractId && c.PlayerCycleStateId == state.Id);
                var attempt = AfterHoursActions.DeliverContract(state, contract, alreadyCompleted, now);
                if (attempt.Receipt is not null)
                {
                    // Same transaction as the cargo, cash and receipt; unique per contract and player.
                    context.AfterHoursBuyerContractCompletions.Add(new BuyerContractCompletion
                    {
                        BuyerContractId = contract.Id,
                        PlayerCycleStateId = state.Id,
                        CompletedAtUtc = now
                    });
                }
                return attempt;
            });

    public Task<AfterHoursActionResult> TrainSkillAsync(string userId, PlayerSkill skill, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.TrainRequest(skill),
            (_, state, now) => Task.FromResult(AfterHoursActions.TrainSkill(state, skill, now)));

    public Task<AfterHoursActionResult> PurchaseGearAsync(string userId, string itemKey, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.PurchaseGearRequest(itemKey),
            (_, state, _) => Task.FromResult(AfterHoursActions.PurchaseGear(state, itemKey)));

    public Task<AfterHoursActionResult> EquipGearAsync(string userId, string itemKey, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.EquipGearRequest(itemKey),
            (_, state, _) => Task.FromResult(AfterHoursActions.EquipGear(state, itemKey)));

    public Task<AfterHoursActionResult> UnequipGearAsync(string userId, string itemKey, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.UnequipGearRequest(itemKey),
            (_, state, _) => Task.FromResult(AfterHoursActions.UnequipGear(state, itemKey)));

    public Task<AfterHoursActionResult> SaveDefenceAsync(string userId, PvpTactic tactic, string? weapon, string? outfit, string? vehicleTool, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.DefenceRequest(tactic, weapon, outfit, vehicleTool),
            (_, state, _) => Task.FromResult(AfterHoursActions.SaveDefence(state, tactic, weapon, outfit, vehicleTool)));

    /// <summary>
    /// One PvP attack. The attacker is the authenticated user; the only identity from the client is
    /// the target's state id. Defender, history, loot, battle and receipt all change in this one
    /// write transaction.
    /// </summary>
    public Task<AfterHoursActionResult> AttackAsync(
        string userId, int defenderStateId, PvpTactic tactic, RiskStance stance,
        string? weapon, string? outfit, string? vehicleTool, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, AfterHoursActions.AttackRequest(defenderStateId, tactic, stance, weapon, outfit, vehicleTool),
            async (context, state, now) =>
            {
                if (new[] { weapon, outfit, vehicleTool }.Any(k => k?.Length > 16))
                    return ActionAttempt.Reject("Unknown item.");

                var defender = await context.AfterHoursPlayerCycleStates
                    .Include(s => s.Cargo)
                    .Include(s => s.Gear)
                    .SingleOrDefaultAsync(s => s.Id == defenderStateId && s.GameCycleId == state.GameCycleId);
                if (defender is null)
                    return ActionAttempt.Reject("That player is not in this cycle.");

                var lastAttack = await context.AfterHoursPvpBattles
                    .Where(b => b.AttackerStateId == state.Id && b.DefenderStateId == defender.Id)
                    .OrderByDescending(b => b.AcceptedAtUtc)
                    .Select(b => (DateTime?)b.AcceptedAtUtc)
                    .FirstOrDefaultAsync();

                return AfterHoursActions.Attack(state, defender, tactic, stance, weapon, outfit, vehicleTool, lastAttack, now, dice.RollPercent);
            });

    private async Task<AfterHoursActionResult> RunAsync(string userId, string idempotencyKey, string request, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
            return new AfterHoursActionResult(null, null, "Invalid request key.");

        return await AfterHoursWriteTransaction.RunAsync(contextFactory, async (context, transaction) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;

            var prior = await context.AfterHoursPlayerActionReceipts
                .AsNoTracking()
                .Include(r => r.PvpBattle)
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey && r.PlayerCycleState!.UserId == userId);
            if (prior is not null)
            {
                var priorState = await context.AfterHoursPlayerCycleStates.AsNoTracking()
                    .Include(s => s.Cargo)
                    .Include(s => s.Gear)
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
                .Include(s => s.Cargo)
                .Include(s => s.Gear)
                .SingleOrDefaultAsync(s => s.GameCycleId == cycle.Id && s.UserId == userId);
            if (state is null)
            {
                // Safe inside the write transaction: no other action can insert it meanwhile.
                state = PlayerCycleState.CreateInitial(cycle.Id, userId, now);
                context.AfterHoursPlayerCycleStates.Add(state);
                await context.SaveChangesAsync();
            }

            var result = await action(context, state, now);
            if (result.Receipt is null)
            {
                // Refused: roll back, including any reconciliation, and report the reconciled view.
                await transaction.RollbackAsync();
                return new AfterHoursActionResult(null, state, result.Error);
            }

            result.Receipt.PlayerCycleStateId = state.Id;
            result.Receipt.IdempotencyKey = idempotencyKey;
            context.AfterHoursPlayerActionReceipts.Add(result.Receipt);
            // Objective progress, completions and rewards for this accepted action: same transaction.
            await ObjectiveTracker.RecordAsync(context, cycle, state, result.Receipt, now);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new AfterHoursActionResult(result.Receipt, state, null);
        });
    }
}
