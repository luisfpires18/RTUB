using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>Write side of the React members area (React track 018; was the Blazor /members/manage). Admin and Owner.</summary>
public interface IMemberAdminService
{
    Task<EventResult<MemberEditDto>> GetForEditAsync(string id, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MentorCandidateDto>>> SearchMentorsAsync(string? query, string? excludeId, ClaimsPrincipal user);

    Task<EventResult<MemberCreatedDto>> CreateAsync(MemberInput input, ClaimsPrincipal user);

    Task<EventResult<MemberEditDto>> UpdateAsync(string id, MemberInput input, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> AddInstrumentAsync(string id, string? instrument, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> RemoveInstrumentAsync(string id, int instrumentId, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> SetPrimaryInstrumentAsync(string id, int instrumentId, ClaimsPrincipal user);

    /// <summary>"Definir alcunha", Leitões only: nickname and username, and an email telling the member.</summary>
    Task<EventResult<bool>> SetNicknameAsync(string id, MemberNicknameInput input, ClaimsPrincipal user);

    /// <summary>Expel (true) or reactivate (false) a Leitão.</summary>
    Task<EventResult<bool>> SetExpelledAsync(string id, bool expelled, ClaimsPrincipal user);

    /// <summary>"Tornar ativo": a retired member, or one on the way back, is active again with the override.</summary>
    Task<EventResult<bool>> MakeActiveAsync(string id, ClaimsPrincipal user);

    /// <summary>The push reminder to a retired member one activity away from coming back.</summary>
    Task<EventResult<bool>> SendReminderAsync(string id, string baseUrl, ClaimsPrincipal user);

    /// <summary>The old hard delete with related data: Owner any member, Admin Leitões only.</summary>
    Task<EventResult<bool>> DeleteAsync(string id, ClaimsPrincipal user);
}
