using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Contact tracking behind the React /events/{id}/contacts (React track 013): who was called about an event,
/// and what they said. Mod and above only, read and write. Every write answers the whole list.
/// </summary>
public interface IEventContactsAdminService
{
    Task<EventResult<EventContactsDto>> GetAsync(int eventId, ClaimsPrincipal user);

    /// <summary>Records (or updates) a call, and an answer syncs the member's enrollment, as before.</summary>
    Task<EventResult<EventContactsDto>> SaveAsync(int eventId, string userId, EventContactInput input, ClaimsPrincipal user);

    /// <summary>Back to "por contactar"; as before, the member's enrollment for the event is removed too.</summary>
    Task<EventResult<EventContactsDto>> ResetAsync(int eventId, string userId, ClaimsPrincipal user);
}
