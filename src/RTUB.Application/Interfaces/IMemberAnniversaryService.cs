using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for member anniversaries (birthdays) filtering
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public interface IMemberAnniversaryService
{
    /// <summary>
    /// Filters members by search term for anniversaries display
    /// </summary>
    /// <param name="members">Collection of members to filter</param>
    /// <param name="searchTerm">Search term to filter by first name, last name, or nickname</param>
    /// <returns>Filtered list of members</returns>
    List<ApplicationUser> FilterAnniversaries(IEnumerable<ApplicationUser> members, string searchTerm);
}
