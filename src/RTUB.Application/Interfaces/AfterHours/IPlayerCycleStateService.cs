using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

public interface IPlayerCycleStateService
{
    /// <summary>
    /// The user's state in the active cycle, created with the fixed starting values on first
    /// call. Idempotent and safe under concurrent first calls. Returns null when no cycle is
    /// active; it never creates a cycle. The returned copy has energy and heat reconciled to now
    /// (not persisted).
    /// </summary>
    Task<PlayerCycleState?> GetOrCreateForActiveCycleAsync(string userId);
}
