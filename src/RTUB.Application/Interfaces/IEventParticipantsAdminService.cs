using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Admin/Owner management of other members' answers behind the React event page (React track 012E,
/// docs/react-events.md): the old /events/{id}/enrollments "Adicionar Membro" and remove. A member's own
/// answer stays the answer modal (<see cref="IEventAgendaService"/>). Writes go through
/// <see cref="IEnrollmentService"/>; every write answers the whole list.
/// </summary>
public interface IEventParticipantsAdminService
{
    Task<EventResult<EventEnrollmentListDto>> GetAsync(int id, ClaimsPrincipal user);

    /// <summary>Up to 20 members matching <paramref name="query"/> who have not answered and are not expelled.</summary>
    Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchMembersAsync(int id, string? query, ClaimsPrincipal user);

    Task<EventResult<EventEnrollmentListDto>> AddAsync(int id, EventEnrollmentAddInput input, ClaimsPrincipal user);

    Task<EventResult<EventEnrollmentListDto>> RemoveAsync(int id, int enrollmentId, ClaimsPrincipal user);
}
