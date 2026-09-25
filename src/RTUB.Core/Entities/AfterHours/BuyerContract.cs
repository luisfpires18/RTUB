using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// One NPC buyer request, open to every player of a cycle for one rotation window. Values are
/// copied from the template when the rotation is created, so later template tuning never changes
/// a contract already offered. One row per cycle, window and slot (unique index).
/// Completion is per player (<see cref="BuyerContractCompletion"/>), never global.
/// </summary>
public class BuyerContract : BaseEntity
{
    public int GameCycleId { get; set; }

    /// <summary>Start of the rotation window this contract belongs to (UTC).</summary>
    public DateTime RotationStartUtc { get; set; }
    public int Slot { get; set; }

    public string TemplateKey { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public CargoType CargoType { get; set; }
    public int Quantity { get; set; }
    public long CashReward { get; set; }
    public long XpReward { get; set; }

    /// <summary>Inclusive, UTC.</summary>
    public DateTime AvailableFromUtc { get; set; }

    /// <summary>Exclusive, UTC.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    public bool IsOpenAt(DateTime utcNow) => AvailableFromUtc <= utcNow && utcNow < ExpiresAtUtc;
}

/// <summary>A player's delivery of a <see cref="BuyerContract"/>. Unique per contract and player state.</summary>
public class BuyerContractCompletion : BaseEntity
{
    public int BuyerContractId { get; set; }
    public int PlayerCycleStateId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}
