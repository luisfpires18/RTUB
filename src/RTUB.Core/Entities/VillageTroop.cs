using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a troop type and count in a village.
/// </summary>
public class VillageTroop : BaseEntity
{
    /// <summary>
    /// FK to the parent Village.
    /// </summary>
    public int VillageId { get; set; }

    /// <summary>
    /// The unit type.
    /// </summary>
    public VillageUnitType UnitType { get; set; }

    /// <summary>
    /// Number of units of this type.
    /// </summary>
    public int Count { get; set; } = 0;

    // Navigation
    public virtual Village Village { get; set; } = null!;

    // Private constructor for EF Core
    private VillageTroop() { }

    /// <summary>
    /// Factory method to create a troop record.
    /// </summary>
    public static VillageTroop Create(int villageId, VillageUnitType unitType, int count = 0)
    {
        return new VillageTroop
        {
            VillageId = villageId,
            UnitType = unitType,
            Count = count
        };
    }
}
