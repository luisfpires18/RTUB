using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for searching members in Orgao Social context
/// Extracted from Questions.razor to improve separation of concerns
/// </summary>
public interface IOrgaoSocialMemberSearchService
{
    /// <summary>
    /// Filters members by search term for Orgao Social member selection
    /// </summary>
    /// <param name="members">Collection of members with group and position info</param>
    /// <param name="searchTerm">Search term to filter by name, nickname, role, or position</param>
    /// <returns>Filtered list of members</returns>
    List<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)> FilterMembers(
        IEnumerable<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)> members,
        string searchTerm);
}
