using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Village game operations.
/// </summary>
public interface IVillageService
{
    /// <summary>
    /// Get the full village state for a user, creating a new village if none exists.
    /// </summary>
    Task<VillageStateDto> GetOrCreateVillageStateAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Tick resources (settle production) and check for completed build jobs.
    /// Called before any mutating action and on periodic sync.
    /// </summary>
    Task<VillageStateDto> TickAndGetStateAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Queue a field upgrade.
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> UpgradeFieldAsync(
        string userId, VillageResourceType resourceType, int slotIndex, CancellationToken ct = default);

    /// <summary>
    /// Construct a new city building (level 0 → 1).
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> ConstructBuildingAsync(
        string userId, VillageBuildingType buildingType, CancellationToken ct = default);

    /// <summary>
    /// Upgrade an existing city building to the next level.
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> UpgradeBuildingAsync(
        string userId, VillageBuildingType buildingType, CancellationToken ct = default);

    /// <summary>
    /// Train troops of a given type.
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> TrainTroopsAsync(
        string userId, VillageUnitType unitType, int count, CancellationToken ct = default);

    /// <summary>
    /// Dispatch a mission with allocated troops.
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> DispatchMissionAsync(
        string userId, string missionKey, VillageMissionDifficulty difficulty,
        Dictionary<VillageUnitType, int> troopAllocation, CancellationToken ct = default);

    /// <summary>
    /// Claim a completed mission's rewards.
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> ClaimMissionAsync(
        string userId, int missionId, CancellationToken ct = default);

    /// <summary>
    /// Dismiss a completed mission without claiming (for failed missions).
    /// </summary>
    Task<(bool Success, string Message, VillageStateDto? State)> DismissMissionAsync(
        string userId, int missionId, CancellationToken ct = default);
}
