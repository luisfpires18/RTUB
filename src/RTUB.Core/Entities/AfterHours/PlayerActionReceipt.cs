using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// The saved outcome of one accepted After Hours action, keyed by the client's idempotency key.
/// Written in the same transaction as the state change it describes, so a retry with the same key
/// returns this row instead of running the action (or its dice rolls) again.
/// Only accepted actions have a receipt; a rejected request changes nothing and saves nothing.
/// </summary>
public class PlayerActionReceipt : BaseEntity
{
    public int PlayerCycleStateId { get; set; }
    public PlayerCycleState? PlayerCycleState { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public PlayerActionKind Action { get; set; }

    /// <summary>
    /// What was asked, e.g. <c>crime:C01:Bold</c> or <c>deposit:100</c>. A key replayed with a
    /// different request is refused instead of returning an unrelated result.
    /// </summary>
    public string Request { get; set; } = string.Empty;

    public string? CrimeId { get; set; }
    public CrimeApproach? Approach { get; set; }

    public bool Succeeded { get; set; }
    public int? SuccessChance { get; set; }
    public int? SuccessRoll { get; set; }
    public bool Jailed { get; set; }
    public DateTime? JailUntilUtc { get; set; }

    public long WalletDelta { get; set; }
    public long BankDelta { get; set; }
    public long XpDelta { get; set; }
    public int HeatDelta { get; set; }
    public int EnergyDelta { get; set; }

    /// <summary>The one cargo type the action moved, if any, and by how much (+ gained, − spent).</summary>
    public CargoType? CargoType { get; set; }
    public int CargoDelta { get; set; }

    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }
}
