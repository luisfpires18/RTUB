using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering rehearsal records
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public class RehearsalFilterService : IRehearsalFilterService
{
    /// <summary>
    /// Filters rehearsals by search term (searches theme, location, and notes)
    /// </summary>
    /// <param name="rehearsals">Collection of rehearsals to filter</param>
    /// <param name="searchTerm">Search term to filter by</param>
    /// <returns>Filtered list of rehearsals</returns>
    public List<Rehearsal> FilterRehearsals(IEnumerable<Rehearsal> rehearsals, string searchTerm)
    {
        var searchHelper = new SearchHelper<Rehearsal> { SearchTerm = searchTerm };
        return searchHelper.FilterMultiple(rehearsals.ToList(), new List<Func<Rehearsal, string>>
        {
            r => r.Theme ?? "",
            r => r.Location ?? "",
            r => r.Notes ?? ""
        });
    }
}
