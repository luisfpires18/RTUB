using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

public interface IPlayerCycleStateService
{
    /// <summary>
    /// The user's state in the active cycle, created with the fixed starting values on first
    /// call. Idempotent and safe under concurrent first calls. Returns null when no cycle is
    /// active; it never creates a cycle.
    /// </summary>
    Task<PlayerCycleState?> GetOrCreateForActiveCycleAsync(string userId);
}
