using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering mentors
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public interface IMemberMentorService
{
    /// <summary>
    /// Filters eligible mentors by search term, excluding the user being edited
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
