using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /hall-of-fame (task 032, docs/react-member-area.md; was the Blazor page): the tuna's twelve records,
/// computed as the old page did. Signed-in members only.
/// </summary>
public interface IHallOfFameService
{
    Task<EventResult<HallOfFameDto>> GetAsync(ClaimsPrincipal user);
}
