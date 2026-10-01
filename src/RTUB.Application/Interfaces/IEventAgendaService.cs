using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React Events area (React track 011). Visibility and every member action are decided here
/// from the caller, never from anything the browser sends. Event management stays with the Blazor
/// /member/events and <see cref="IEventService"/>.
/// </summary>
public interface IEventAgendaService
{
    Task<EventAgendaDto> GetAgendaAsync(ClaimsPrincipal user);

    /// <summary>The next <paramref name="count"/> events of the agenda, visitors' fields only (the home preview).</summary>
    Task<IReadOnlyList<UpcomingEventDto>> GetUpcomingPreviewAsync(int count);

    /// <summary>One event, or null when it does not exist.</summary>
    Task<EventDetailDto?> GetEventAsync(int id, ClaimsPrincipal user);

    Task<EventResult<EventEnrollmentDto>> GetEnrollmentAsync(int id, ClaimsPrincipal user);

    Task<EventResult<EventEnrollmentDto>> SaveEnrollmentAsync(int id, EventEnrollmentInput input, ClaimsPrincipal user);

    Task<EventResult<EventEnrollmentDto>> RemoveEnrollmentAsync(int id, ClaimsPrincipal user);

    /// <summary>Audits a video play, as the Blazor page did; false when the video does not exist.</summary>
    Task<bool> RecordVideoPlayAsync(int videoId, ClaimsPrincipal user);
}
