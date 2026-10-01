using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Admin/Owner repertoire management behind the React event page (React track 012D,
/// docs/react-events.md). Writes go through <see cref="IEventRepertoireService"/>, as the old
/// /member/events modal did; every write answers the whole repertoire.
/// </summary>
public interface IEventRepertoireAdminService
{
    Task<EventResult<EventRepertoireManageDto>> GetAsync(int id, ClaimsPrincipal user);

    /// <summary>Up to 20 songs whose title matches <paramref name="query"/>, visible to the caller and not yet in the event.</summary>
    Task<EventResult<IReadOnlyList<EventRepertoireSongDto>>> SearchSongsAsync(int id, string? query, ClaimsPrincipal user);

    Task<EventResult<EventRepertoireManageDto>> AddAsync(int id, EventRepertoireAddInput input, ClaimsPrincipal user);

    Task<EventResult<EventRepertoireManageDto>> RemoveAsync(int id, int itemId, ClaimsPrincipal user);

    /// <summary>Removes every song of one day, as the old modal's "apagar dia" did.</summary>
    Task<EventResult<EventRepertoireManageDto>> RemoveDayAsync(int id, string? date, ClaimsPrincipal user);

    Task<EventResult<EventRepertoireManageDto>> ReorderAsync(int id, EventRepertoireOrderInput input, ClaimsPrincipal user);
}
