using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>
/// Outcome of an action request. Accepted (new or <see cref="Replayed"/>) carries the saved
/// <see cref="Receipt"/>; a refusal carries <see cref="Error"/> and changed nothing.
/// <see cref="State"/> is the player's state reconciled to now, when there is one.
/// </summary>
public sealed record AfterHoursActionResult(
    PlayerActionReceipt? Receipt,
    PlayerCycleState? State,
    string? Error,
    bool Replayed = false)
{
    public bool Accepted => Error is null;
}

/// <summary>
/// Server-authoritative After Hours actions. Every call needs a client-generated idempotency key
/// (≤ 64 chars): the same key always yields the same accepted outcome, never a second one.
/// </summary>
public interface IAfterHoursActionService
{
    Task<AfterHoursActionResult> CommitCrimeAsync(string userId, string crimeId, CrimeApproach approach, string idempotencyKey);
    Task<AfterHoursActionResult> TakeCoverJobAsync(string userId, string idempotencyKey);
    Task<AfterHoursActionResult> DepositAsync(string userId, long amount, string idempotencyKey);
    Task<AfterHoursActionResult> WithdrawAsync(string userId, long amount, string idempotencyKey);
    Task<AfterHoursActionResult> SellToFenceAsync(string userId, CargoType cargo, int quantity, string idempotencyKey);
    Task<AfterHoursActionResult> DeliverContractAsync(string userId, int buyerContractId, string idempotencyKey);
    Task<AfterHoursActionResult> TrainSkillAsync(string userId, PlayerSkill skill, string idempotencyKey);
    Task<AfterHoursActionResult> PurchaseGearAsync(string userId, string itemKey, string idempotencyKey);
    Task<AfterHoursActionResult> EquipGearAsync(string userId, string itemKey, string idempotencyKey);
    Task<AfterHoursActionResult> UnequipGearAsync(string userId, string itemKey, string idempotencyKey);
}

/// <summary>Server-side dice. Never seeded or driven by a client.</summary>
public interface IAfterHoursDice
{
    /// <summary>A uniform roll, 1..100 inclusive.</summary>
    int RollPercent();
}
