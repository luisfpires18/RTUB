using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an active construction job in a village.
/// Only one build job can be active at a time.
/// </summary>
public class VillageBuildJob : BaseEntity
{
    /// <summary>
    /// FK to the parent Village.
    /// </summary>
    public int VillageId { get; set; }

    /// <summary>
    /// Whether this is a field upgrade or a city building.
    /// True = field upgrade, False = city building.
    /// </summary>
    public bool IsFieldUpgrade { get; set; }

    /// <summary>
    /// For city buildings: the building type being constructed/upgraded.
    /// </summary>
    public VillageBuildingType? BuildingType { get; set; }

    /// <summary>
    /// For field upgrades: the resource type of the field.
    /// </summary>
    public VillageResourceType? FieldResourceType { get; set; }

    /// <summary>
    /// For field upgrades: the slot index of the field.
    /// </summary>
    public int? FieldSlotIndex { get; set; }

    /// <summary>
    /// The target level being built to.
    /// </summary>
    public int ToLevel { get; set; }

    /// <summary>
    /// When construction started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public int DurationSecs { get; set; }

    /// <summary>
    /// When construction finishes.
    /// </summary>
    public DateTime FinishesAt { get; set; }

    /// <summary>
    /// Current status of the build job.
    /// </summary>
    public VillageBuildJobStatus Status { get; set; } = VillageBuildJobStatus.Active;

    // Navigation
    public virtual Village Village { get; set; } = null!;

    // Private constructor for EF Core
    private VillageBuildJob() { }

    /// <summary>
    /// Factory for a city building job.
    /// </summary>
    public static VillageBuildJob CreateBuildingJob(int villageId, VillageBuildingType buildingType, int toLevel, int durationSecs)
    {
        var now = DateTime.UtcNow;
        return new VillageBuildJob
        {
            VillageId = villageId,
            IsFieldUpgrade = false,
            BuildingType = buildingType,
            ToLevel = toLevel,
            StartedAt = now,
            DurationSecs = durationSecs,
            FinishesAt = now.AddSeconds(durationSecs),
            Status = VillageBuildJobStatus.Active
        };
    }

    /// <summary>
    /// Factory for a field upgrade job.
    /// </summary>
    public static VillageBuildJob CreateFieldJob(int villageId, VillageResourceType resourceType, int slotIndex, int toLevel, int durationSecs)
    {
        var now = DateTime.UtcNow;
        return new VillageBuildJob
        {
            VillageId = villageId,
            IsFieldUpgrade = true,
            FieldResourceType = resourceType,
            FieldSlotIndex = slotIndex,
            ToLevel = toLevel,
            StartedAt = now,
            DurationSecs = durationSecs,
            FinishesAt = now.AddSeconds(durationSecs),
            Status = VillageBuildJobStatus.Active
        };
    }

    /// <summary>
    /// Whether this job has finished based on current time.
    /// </summary>
    public bool IsFinished => DateTime.UtcNow >= FinishesAt;

    /// <summary>
    /// Remaining seconds until completion (0 if finished).
    /// </summary>
    public double RemainingSeconds => Math.Max(0, (FinishesAt - DateTime.UtcNow).TotalSeconds);

    /// <summary>
    /// Progress as a fraction 0.0–1.0.
    /// </summary>
    public double Progress => DurationSecs <= 0 ? 1.0 : Math.Min(1.0, 1.0 - RemainingSeconds / DurationSecs);
}
