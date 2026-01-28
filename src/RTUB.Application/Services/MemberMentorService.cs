using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering mentors
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public class MemberMentorService : IMemberMentorService
{
    /// <summary>
    /// Filters eligible mentors by search term, excluding the user being edited
    /// </summary>
    /// <param name="eligibleMentors">Collection of eligible mentors to filter</param>
    /// <param name="searchTerm">Search term to filter by first name, last name, or nickname</param>
    /// <param name="excludeUserId">User ID to exclude from results (e.g., the user being edited)</param>
    /// <returns>Filtered list of mentors</returns>
    public List<ApplicationUser> FilterMentors(
        IEnumerable<ApplicationUser> eligibleMentors,
        string searchTerm,
        string? excludeUserId = null)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return new List<ApplicationUser>();
        }

        // Exclude the user being edited from mentor candidates
        var availableMentors = eligibleMentors;
        if (!string.IsNullOrEmpty(excludeUserId))
        {
            availableMentors = eligibleMentors.Where(u => u.Id != excludeUserId).ToList();
        }
        
        var searchHelper = new SearchHelper<ApplicationUser> { SearchTerm = searchTerm };
        return searchHelper.FilterMultiple(availableMentors.ToList(), new List<Func<ApplicationUser, string>>
        {
            u => u.FirstName ?? "",
            u => u.LastName ?? "",
            u => $"{u.FirstName} {u.LastName}",
            u => u.Nickname ?? ""
        }, caseSensitive: false, accentSensitive: false);
    }
}
