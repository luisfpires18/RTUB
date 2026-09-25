using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>Read-only yearbook: completed archives only, exactly as stored; and a player's cosmetic titles.</summary>
public interface IYearbookService
{
    /// <summary>This player's active (not revoked) cosmetic awards, newest first.</summary>
    Task<IReadOnlyList<AfterHoursCosmeticAward>> GetActiveAwardsAsync(string userId);

    /// <summary>Every archive, newest first, with its player entries, family entries and rosters.</summary>
    Task<IReadOnlyList<CycleArchive>> GetArchivesAsync();
}
