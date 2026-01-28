using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering album members (exclusive album feature)
/// Extracted from Albums.razor to improve separation of concerns
/// </summary>
public class AlbumFilterService : IAlbumFilterService
{
    public List<ApplicationUser> FilterAvailableMembersForExclusive(
        IEnumerable<ApplicationUser> allMembers,
        IEnumerable<string> selectedMemberIds,
        string searchTerm,
        int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<ApplicationUser>();
        }

        // Get list of already selected user IDs
        var selectedUserIdsSet = selectedMemberIds.ToHashSet();

        // Filter members who are not already selected
        var search = searchTerm.ToLower();
        return allMembers
            .Where(u => !selectedUserIdsSet.Contains(u.Id))
            .Where(u => (u.FirstName?.ToLower().Contains(search) ?? false) ||
                       (u.LastName?.ToLower().Contains(search) ?? false) ||
                       (u.Nickname?.ToLower().Contains(search) ?? false))
            .OrderBy(u => u.FirstName)
            .Take(maxResults)
            .ToList();
    }
}
