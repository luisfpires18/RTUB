using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for searching members in Orgao Social context
/// Extracted from Questions.razor to improve separation of concerns
/// </summary>
public class OrgaoSocialMemberSearchService : IOrgaoSocialMemberSearchService
{
    /// <summary>
    /// Filters members by search term for Orgao Social member selection
    /// </summary>
    /// <param name="members">Collection of members with group and position info</param>
    /// <param name="searchTerm">Search term to filter by name, nickname, role, or position</param>
    /// <returns>Filtered list of members</returns>
    public List<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)> FilterMembers(
        IEnumerable<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)> members,
        string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return members.ToList();
        }

        var search = searchTerm.ToLower();
        return members
            .Where(item =>
                (item.Member.FirstName?.ToLower().Contains(search) ?? false) ||
                (item.Member.LastName?.ToLower().Contains(search) ?? false) ||
                ($"{item.Member.FirstName ?? ""} {item.Member.LastName ?? ""}".ToLower().Contains(search)) ||
                (item.Member.Nickname?.ToLower().Contains(search) ?? false) ||
                (PositionHelper.GetDisplayName(item.Position).ToLower().Contains(search)) ||
                (OrgaoSocialHelper.GetDisplayName(item.Group).ToLower().Contains(search)) ||
                ($"{OrgaoSocialHelper.GetDisplayName(item.Group)} {PositionHelper.GetDisplayName(item.Position)}".ToLower().Contains(search)))
            .ToList();
    }
}
