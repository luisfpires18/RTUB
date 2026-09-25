using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// How much of one cargo type a player holds in one cycle. One row per state and type (unique
/// index), never negative (check constraint). Belongs to the cycle's state, so it resets with it.
/// </summary>
public class PlayerCargo : BaseEntity
{
    public int PlayerCycleStateId { get; set; }
    public CargoType CargoType { get; set; }
    public int Quantity { get; set; }
}
