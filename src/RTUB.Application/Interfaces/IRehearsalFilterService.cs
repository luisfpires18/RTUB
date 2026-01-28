using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering rehearsal records
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public interface IRehearsalFilterService
{
    /// <summary>
    /// Filters rehearsals by search term (searches theme, location, and notes)
    /// </summary>
    /// <param name="rehearsals">Collection of rehearsals to filter</param>
    /// <param name="searchTerm">Search term to filter by</param>
    /// <returns>Filtered list of rehearsals</returns>
    List<Rehearsal> FilterRehearsals(IEnumerable<Rehearsal> rehearsals, string searchTerm);
}
