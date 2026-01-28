using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering mentors in profile pages
/// Extracted from Profile.razor to improve separation of concerns
/// </summary>
public interface IProfileMentorService
{
    /// <summary>
    /// Filters eligible mentors by search term, excluding the current user
    /// </summary>
    /// <param name="eligibleMentors">Collection of eligible mentors to filter</param>
    /// <param name="searchTerm">Search term to filter by first name, last name, or nickname</param>
    /// <param name="excludeUserId">User ID to exclude from results (e.g., the user being edited)</param>
    /// <returns>Filtered list of mentors</returns>
    List<ApplicationUser> FilterMentors(
        IEnumerable<ApplicationUser> eligibleMentors,
        string searchTerm,
        string? excludeUserId = null);
}
