using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /hall-of-fame (docs/react-member-area.md): the tuna's twelve records,
/// computed. Signed-in members only.
/// </summary>
public interface IHallOfFameService
{
    Task<EventResult<HallOfFameDto>> GetAsync(ClaimsPrincipal user);
}
