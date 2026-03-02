using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a dispatched or completed mission in a village.
/// </summary>
public class VillageMission : BaseEntity
{
    /// <summary>
    /// FK to the parent Village.
    /// </summary>
    public int VillageId { get; set; }

    /// <summary>
    /// Mission template key (e.g., "ark_ridge_wolves").
    /// </summary>
    public string MissionKey { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the mission.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Difficulty level chosen for this mission.
    /// </summary>
    public VillageMissionDifficulty Difficulty { get; set; } = VillageMissionDifficulty.Easy;

    /// <summary>
    /// Current status of the mission.
    /// </summary>
    public VillageMissionStatus Status { get; set; } = VillageMissionStatus.Ongoing;

    /// <summary>
    /// JSON of the troops sent: { "Legionary": 5, "Marksman": 3 }
    /// </summary>
    public string SentTroopsJson { get; set; } = "{}";

    /// <summary>
    /// When the mission was dispatched.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the mission finishes.
    /// </summary>
    public DateTime FinishesAt { get; set; }

    /// <summary>
    /// JSON of the reward resources (populated on completion).
    /// </summary>
    public string? RewardJson { get; set; }

    /// <summary>
    /// JSON of troop losses (populated on completion).
    /// </summary>
    public string? LossesJson { get; set; }

    /// <summary>
    /// Whether the reward has been claimed by the player.
    /// </summary>
    public bool Claimed { get; set; } = false;

    // Navigation
    public virtual Village Village { get; set; } = null!;

    // Private constructor for EF Core
    private VillageMission() { }

    /// <summary>
    /// Factory method to create a new ongoing mission.
    /// </summary>
    public static VillageMission Create(
        int villageId,
        string missionKey,
        string name,
        VillageMissionDifficulty difficulty,
        string sentTroopsJson,
        int durationSecs)
    {
        var now = DateTime.UtcNow;
        return new VillageMission
        {
            VillageId = villageId,
            MissionKey = missionKey,
            Name = name,
            Difficulty = difficulty,
            Status = VillageMissionStatus.Ongoing,
            SentTroopsJson = sentTroopsJson,
            StartedAt = now,
            FinishesAt = now.AddSeconds(durationSecs)
        };
    }

    /// <summary>
    /// Whether this mission has finished based on current time.
    /// </summary>
    public bool IsFinished => DateTime.UtcNow >= FinishesAt;

    /// <summary>
    /// Remaining seconds until completion.
    /// </summary>
    public double RemainingSeconds => Math.Max(0, (FinishesAt - DateTime.UtcNow).TotalSeconds);
}
