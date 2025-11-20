using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Rehearsal entity with domain-specific operations
/// </summary>
public interface IRehearsalRepository : IRepository<Rehearsal>
{
    /// <summary>
    /// Gets a rehearsal by specific date
    /// </summary>
    Task<Rehearsal?> GetRehearsalByDateAsync(DateTime date);

    /// <summary>
    /// Gets rehearsals within a date range
    /// </summary>
    Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets upcoming rehearsals ordered by date
    /// </summary>
    Task<IEnumerable<Rehearsal>> GetUpcomingRehearsalsAsync(int count = 10);
}
