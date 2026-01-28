using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering active members by status and search
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public class ActiveMemberFilterService : IActiveMemberFilterService
{
    /// <summary>
    /// Filters active members by status and search term
    /// </summary>
    /// <param name="activeMembers">Collection of active member data to filter</param>
    /// <param name="statusFilter">Status filter: "active", "retired", or empty string for all</param>
    /// <param name="searchTerm">Search term to filter by name or nickname</param>
    /// <returns>Filtered list of active members</returns>
    public List<ActiveMemberData> FilterActiveMembers(
        IEnumerable<ActiveMemberData> activeMembers,
        string statusFilter,
        string searchTerm)
    {
        var membersToFilter = activeMembers.ToList();
        
        // Apply status filter first
        if (!string.IsNullOrEmpty(statusFilter))
        {
            if (statusFilter == "active")
            {
                // Show members who are not retired (either by statusData or by member.IsRetired)
                membersToFilter = membersToFilter.Where(m => 
                    m.StatusData?.IsRetired == false || 
                    (m.StatusData == null && !m.Member.IsRetired)).ToList();
            }
            else if (statusFilter == "retired")
            {
                // Show members who are retired (either by statusData or by member.IsRetired)
                membersToFilter = membersToFilter.Where(m => 
                    m.StatusData?.IsRetired == true || 
                    (m.StatusData == null && m.Member.IsRetired)).ToList();
            }
        }

        // Apply search filter
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return membersToFilter;
        }
        
        var searchHelper = new SearchHelper<ActiveMemberData> { SearchTerm = searchTerm };
        return searchHelper.FilterMultiple(membersToFilter, new List<Func<ActiveMemberData, string>>
        {
            m => m.Member.FirstName ?? string.Empty,
            m => m.Member.LastName ?? string.Empty,
            m => $"{m.Member.FirstName} {m.Member.LastName}",
            m => m.Member.Nickname ?? string.Empty
        }, caseSensitive: false, accentSensitive: false);
    }
}
