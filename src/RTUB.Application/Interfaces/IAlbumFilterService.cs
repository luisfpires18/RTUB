using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering album members (exclusive album feature)
/// Extracted from Albums.razor to improve separation of concerns
/// </summary>
public interface IAlbumFilterService
{
    /// <summary>
    /// Filters available members for exclusive album selection
    /// Excludes already selected members and filters by search term
    /// </summary>
    /// <param name="allMembers">All available members</param>
    /// <param name="selectedMemberIds">IDs of already selected members to exclude</param>
    /// <param name="searchTerm">Search term to filter by (name, nickname)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>Filtered list of available members</returns>
    List<ApplicationUser> FilterAvailableMembersForExclusive(
        IEnumerable<ApplicationUser> allMembers,
        IEnumerable<string> selectedMemberIds,
        string searchTerm,
        int maxResults = 10);
}
