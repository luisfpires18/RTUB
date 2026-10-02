using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React Events area (React track 011). Visibility and every member action are decided here
/// from the caller, never from anything the browser sends. Writes go through <see cref="IEventService"/>
/// and <see cref="IEnrollmentService"/>.
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

    /// <summary>One event for the Admin/Owner edit form.</summary>
    Task<EventResult<EventEditDto>> GetEventForEditAsync(int id, ClaimsPrincipal user);

    /// <summary>Creates an event (Admin/Owner) and announces it by push, as the old form did; <paramref name="baseUrl"/> builds the push link.</summary>
    Task<EventResult<EventSummaryDto>> CreateEventAsync(EventInput input, ClaimsPrincipal user, string baseUrl);

    /// <summary>Updates an event's details (Admin/Owner); image, cancellation and answers are kept.</summary>
    Task<EventResult<EventSummaryDto>> UpdateEventAsync(int id, EventInput input, ClaimsPrincipal user);

    /// <summary>
    /// Deletes an event (Admin/Owner), as the old page did: the row and its image go, and the database
    /// cascades its enrollments, prizes, videos, repertoire, discussion and contacts. Refused (InUse) when
    /// NERBA orders point at it.
    /// </summary>
    Task<EventResult<bool>> DeleteEventAsync(int id, ClaimsPrincipal user);

    /// <summary>
    /// Members' enrollment statistics for events dated <paramref name="from"/>..<paramref name="to"/> (default: the
    /// current season, September to August). Signed-in members only.
    /// </summary>
    Task<EventResult<EventStatsDto>> GetStatsAsync(DateOnly? from, DateOnly? to, ClaimsPrincipal user);

    /// <summary>Audits a video play, as the Blazor page did; false when the video does not exist.</summary>
    Task<bool> RecordVideoPlayAsync(int videoId, ClaimsPrincipal user);
}
