using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /members/map (task 032, docs/react-member-area.md; was the Blazor /member/map): members grouped by the
/// city on their profile, placed by the geocoding cache. City-level only. Signed-in members only.
/// </summary>
public interface IMemberMapService
{
    Task<EventResult<MemberMapDto>> GetAsync(ClaimsPrincipal user);
}
