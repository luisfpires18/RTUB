using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a constructed building in a village.
/// </summary>
public class VillageBuilding : BaseEntity
{
    /// <summary>
    /// FK to the parent Village.
    /// </summary>
    public int VillageId { get; set; }

    /// <summary>
    /// The type of building.
    /// </summary>
    public VillageBuildingType BuildingType { get; set; }

    /// <summary>
    /// Current level of this building (1+).
    /// </summary>
    public int Level { get; set; } = 0;

    // Navigation
    public virtual Village Village { get; set; } = null!;

    // Private constructor for EF Core
    private VillageBuilding() { }

    /// <summary>
    /// Factory method to create a new building.
    /// </summary>
    public static VillageBuilding Create(int villageId, VillageBuildingType buildingType, int level = 0)
    {
        return new VillageBuilding
        {
            VillageId = villageId,
            BuildingType = buildingType,
            Level = level
        };
    }
}
