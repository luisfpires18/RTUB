using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>Read side of the React members area (React track 017): directory, details, active members, birthdays, hierarchy.</summary>
public interface IMemberDirectoryService
{
    Task<EventResult<MemberDirectoryDto>> GetDirectoryAsync(MemberDirectoryQuery query, ClaimsPrincipal user);

    Task<EventResult<MemberDetailDto>> GetMemberAsync(string id, ClaimsPrincipal user);

    /// <summary><paramref name="status"/>: "", "active" or "retired".</summary>
    Task<EventResult<IReadOnlyList<ActiveMemberDto>>> GetActiveMembersAsync(string? status, string? search, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberBirthdayDto>>> GetBirthdaysAsync(string? search, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberTreeNodeDto>>> GetHierarchyAsync(ClaimsPrincipal user);
}
