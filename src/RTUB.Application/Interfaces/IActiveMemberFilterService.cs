using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering active members by status and search
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public interface IActiveMemberFilterService
{
    /// <summary>
    /// Filters active members by status and search term
    /// </summary>
    /// <param name="activeMembers">Collection of active member data to filter</param>
    /// <param name="statusFilter">Status filter: "active", "retired", or empty string for all</param>
    /// <param name="searchTerm">Search term to filter by name or nickname</param>
    /// <returns>Filtered list of active members</returns>
    List<ActiveMemberData> FilterActiveMembers(
        IEnumerable<ActiveMemberData> activeMembers,
        string statusFilter,
        string searchTerm);
}
