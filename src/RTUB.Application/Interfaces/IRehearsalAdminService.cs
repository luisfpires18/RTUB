using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Rehearsal management behind the React pages (React track 014): Admin or Owner only, enforced here.
/// Writes go through <see cref="IRehearsalService"/> and <see cref="IRehearsalAttendanceService"/>, so their
/// side effects (approval push, retirement status, category snapshot) are unchanged.
/// </summary>
public interface IRehearsalAdminService
{
    Task<EventResult<RehearsalSummaryDto>> CreateAsync(RehearsalInput input, ClaimsPrincipal user);

    Task<EventResult<RehearsalRangeResultDto>> CreateRangeAsync(RehearsalRangeInput input, ClaimsPrincipal user);

    Task<EventResult<RehearsalSummaryDto>> UpdateAsync(int id, RehearsalInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);

    /// <summary>Cancels (reason required) and, as before, deletes every presença; no email is sent.</summary>
    Task<EventResult<bool>> CancelAsync(int id, RehearsalCancelInput input, ClaimsPrincipal user);

    /// <summary>Reactivates an upcoming cancelled rehearsal; presenças are not restored.</summary>
    Task<EventResult<bool>> ReactivateAsync(int id, ClaimsPrincipal user);

    Task<EventResult<RehearsalNoticeAudienceDto>> GetNoticeAudienceAsync(int id, ClaimsPrincipal user);

    Task<EventResult<RehearsalNoticeResultDto>> SendNoticeAsync(int id, RehearsalNoticeInput input, ClaimsPrincipal user, string baseUrl);

    /// <summary>Approves a pending presença once the rehearsal can be approved (the member gets a push, as before).</summary>
    Task<EventResult<bool>> ApproveAsync(int id, int attendanceId, ClaimsPrincipal user);

    /// <summary>Admin/Owner once the rehearsal can be approved; anyone their own presença.</summary>
    Task<EventResult<bool>> RemoveAttendanceAsync(int id, int attendanceId, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchMembersAsync(int id, string? query, ClaimsPrincipal user);

    /// <summary>Adds a member's presença, approved, with their primary instrument, once the rehearsal can be approved.</summary>
    Task<EventResult<bool>> AddAttendeeAsync(int id, RehearsalAttendeeInput input, ClaimsPrincipal user);
}
