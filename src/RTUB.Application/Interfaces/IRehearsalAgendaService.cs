using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React Rehearsals area for members (React track 014): the list, one rehearsal with its presenças, the
/// caller's own presença and the statistics. Every rule is decided here from the caller; presença writes go
/// through <see cref="IRehearsalAttendanceService"/>, so its push notifications and category snapshot are unchanged.
/// </summary>
public interface IRehearsalAgendaService
{
    Task<EventResult<RehearsalAgendaDto>> GetAgendaAsync(ClaimsPrincipal user);

    Task<EventResult<RehearsalDetailDto>> GetAsync(int id, ClaimsPrincipal user);

    Task<EventResult<RehearsalAttendanceDto>> GetMyAttendanceAsync(int id, ClaimsPrincipal user);

    /// <summary>Vou / Não vou on a rehearsal that is not over and not cancelled.</summary>
    Task<EventResult<RehearsalAttendanceDto>> SaveMyAttendanceAsync(int id, RehearsalAttendanceInput input, ClaimsPrincipal user);

    /// <summary>Removes the caller's own presença (any state, as the old list allowed).</summary>
    Task<EventResult<RehearsalAttendanceDto>> RemoveMyAttendanceAsync(int id, ClaimsPrincipal user);

    Task<EventResult<RehearsalStatsDto>> GetStatsAsync(DateOnly? from, DateOnly? to, ClaimsPrincipal user);
}
