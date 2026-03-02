using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a single resource field tile in a village.
/// Each village has 12 fields: 4 wood, 4 stone, 4 food.
/// </summary>
public class VillageField : BaseEntity
{
    /// <summary>
    /// FK to the parent Village.
    /// </summary>
    public int VillageId { get; set; }

    /// <summary>
    /// The resource type this field produces.
    /// </summary>
    public VillageResourceType ResourceType { get; set; }

    /// <summary>
    /// Slot index within the resource type group (0-based).
    /// Wood/Stone/Food: 0-3.
    /// </summary>
    public int SlotIndex { get; set; }

    /// <summary>
    /// Current level of this field (0 = not upgraded).
    /// </summary>
    public int Level { get; set; } = 0;

    // Navigation
    public virtual Village Village { get; set; } = null!;

    // Private constructor for EF Core
    private VillageField() { }

    /// <summary>
    /// Factory method to create a new field tile.
    /// </summary>
    public static VillageField Create(int villageId, VillageResourceType resourceType, int slotIndex)
    {
        return new VillageField
        {
            VillageId = villageId,
            ResourceType = resourceType,
            SlotIndex = slotIndex,
            Level = 0
        };
    }
}
