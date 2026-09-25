using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>Read-only yearbook: completed archives only, exactly as stored.</summary>
public interface IYearbookService
{
    /// <summary>Every archive, newest first, with its player entries, family entries and rosters.</summary>
    Task<IReadOnlyList<CycleArchive>> GetArchivesAsync();
}
