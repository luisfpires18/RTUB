using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /profile editor (task 032, docs/react-member-area.md; was the Blazor /member/profile): the signed-in
/// member reads and edits their own profile, and only their own. Every rule is decided here, from the session.
/// </summary>
public interface IMyProfileService
{
    Task<EventResult<MyProfileDto>> GetAsync(ClaimsPrincipal user);

    Task<EventResult<MyProfileDto>> UpdatePersonalAsync(MyPersonalInput input, ClaimsPrincipal user);

    Task<EventResult<MyProfileDto>> UpdateTunaAsync(MyTunaInput input, ClaimsPrincipal user);

    /// <summary>Padrinho candidates: Tunos and above, never the member themselves.</summary>
    Task<EventResult<IReadOnlyList<MentorCandidateDto>>> SearchMentorsAsync(string? query, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> AddInstrumentAsync(string? instrument, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> RemoveInstrumentAsync(int instrumentId, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> SetPrimaryInstrumentAsync(int instrumentId, ClaimsPrincipal user);

    Task<EventResult<MyPhotoDto>> SetPhotoAsync(MyPhotoUpload photo, ClaimsPrincipal user);

    Task<EventResult<bool>> SetSubscribedAsync(bool subscribed, ClaimsPrincipal user);

    /// <summary>Changes the password and clears RequirePasswordChange; the caller then refreshes the sign-in.</summary>
    Task<EventResult<bool>> ChangePasswordAsync(MyPasswordInput input, ClaimsPrincipal user);
}
