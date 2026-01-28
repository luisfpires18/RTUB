using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for member anniversaries (birthdays) filtering
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public class MemberAnniversaryService : IMemberAnniversaryService
{
    /// <summary>
    /// Filters members by search term for anniversaries display
    /// </summary>
    /// <param name="members">Collection of members to filter</param>
    /// <param name="searchTerm">Search term to filter by first name, last name, or nickname</param>
    /// <returns>Filtered list of members</returns>
    public List<ApplicationUser> FilterAnniversaries(IEnumerable<ApplicationUser> members, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return members.ToList();
        }
        
        var searchHelper = new SearchHelper<ApplicationUser> { SearchTerm = searchTerm };
        return searchHelper.FilterMultiple(members.ToList(), new List<Func<ApplicationUser, string>>
        {
            u => u.FirstName ?? string.Empty,
            u => u.LastName ?? string.Empty,
            u => $"{u.FirstName} {u.LastName}",
            u => u.Nickname ?? string.Empty
        }, caseSensitive: false, accentSensitive: false);
    }
}
