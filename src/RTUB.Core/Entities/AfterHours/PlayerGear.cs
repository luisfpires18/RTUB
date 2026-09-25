using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// A gear item a player bought in one cycle; owned until the cycle ends. One row per state and
/// item (unique index). Which item is equipped lives on <see cref="PlayerCycleState"/>, one column
/// per slot, so "one equipped per slot" holds by construction. Slot and tier are copied from the
/// catalogue for queries.
/// </summary>
public class PlayerGear : BaseEntity
{
    public int PlayerCycleStateId { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public GearSlot Slot { get; set; }
    public int Tier { get; set; }
}
