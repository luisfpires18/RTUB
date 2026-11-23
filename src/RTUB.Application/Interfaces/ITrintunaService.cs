using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The React /trintuna (docs/react-trintuna.md). Rules: <see cref="Helpers.TrintunaAuthorization"/>.</summary>
public interface ITrintunaService
{
    /// <summary>Whether the member has a Trintuna access row (the menu link; Owner is added by the caller).</summary>
    Task<bool> HasAccessAsync(string userId);

    Task<EventResult<TrintunaBoardDto>> GetAsync(ClaimsPrincipal user);

    Task<EventResult<GatheringDto>> CreateAsync(GatheringInput input, ClaimsPrincipal user);

    /// <summary>Owner, or the gathering's organizer (number and organizer then stay as stored).</summary>
    Task<EventResult<GatheringDto>> UpdateAsync(int id, GatheringInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);

    /// <summary>The only way to date a gathering: one of its own poll options. Closes the poll.</summary>
    Task<EventResult<GatheringDto>> SettleAsync(int id, GatheringOptionInput input, ClaimsPrincipal user);

    Task<EventResult<GatheringDto>> VoteAsync(int id, GatheringOptionInput input, ClaimsPrincipal user);

    Task<EventResult<GatheringDto>> SetPresenceAsync(int id, GatheringPresenceInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> ClearPresenceAsync(int id, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<TrintunaMemberDto>>> GetAccessAsync(ClaimsPrincipal user);

    /// <summary>Replaces the list. Unknown or expelled ids are refused (400).</summary>
    Task<EventResult<IReadOnlyList<TrintunaMemberDto>>> UpdateAccessAsync(TrintunaAccessInput input, ClaimsPrincipal user);
}
