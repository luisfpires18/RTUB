using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Meeting board: access gates and visible meetings.
public sealed partial class MeetingBoardService
{
    // ---------- access ----------

    /// <summary>The session's member, or why not: a visitor must sign in, a Leitão is refused everything.</summary>
    private async Task<(MeetingAccess? Access, EventResultStatus Refusal)> AccessAsync(ClaimsPrincipal principal)
    {
        var userId = principal.Identity?.IsAuthenticated == true ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        if (string.IsNullOrEmpty(userId))
        {
            return (null, EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        var access = new MeetingAccess(user, principal.IsInRole("Owner"), principal.IsInRole("Admin"));
        return access.IsBlocked ? (null, EventResultStatus.Forbidden) : (access, EventResultStatus.Ok);
    }

    /// <summary>The meetings this member sees: the service filter (CV, Direção, Leitão).</summary>
    private async Task<List<Meeting>> VisibleMeetingsAsync(MeetingAccess access) =>
        (await _meetings.GetAllMeetingsAsync(null, 1, int.MaxValue, access.UserId)).ToList();

    private async Task<Meeting?> VisibleMeetingAsync(MeetingAccess access, int id) =>
        (await VisibleMeetingsAsync(access)).FirstOrDefault(m => m.Id == id);

    /// <summary>A meeting whose email, push or cancel the member may send: theirs to manage, upcoming, not cancelled.</summary>
    private async Task<(Meeting? Meeting, EventResultStatus Refusal)> NotifiableAsync(MeetingAccess access, int id)
    {
        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        if (!access.CanManage(meeting))
        {
            return (null, EventResultStatus.Forbidden);
        }

        return meeting.IsCancelled || meeting.Date.Date < Today
            ? (null, EventResultStatus.Closed)
            : (meeting, EventResultStatus.Ok);
    }

    /// <summary>A meeting whose participants list may be opened: seen by the member and not cancelled.</summary>
    private async Task<(Meeting? Meeting, EventResultStatus Refusal)> ParticipantsMeetingAsync(MeetingAccess access, int id)
    {
        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        return meeting.IsCancelled ? (null, EventResultStatus.Closed) : (meeting, EventResultStatus.Ok);
    }

    private sealed record AtaContext(Meeting Meeting, MeetingAta? Existing, bool CanWrite);

    /// <summary>
    /// A meeting whose ata may be touched: seen, not cancelled, on or after its day. <c>CanWrite</c> is the old
    /// <c>CanCreateOrEditAta</c> knows a meeting's ata only from the day after, so on the
    /// day itself the secretaries named on an ata are not yet counted.
    /// </summary>
    private async Task<(AtaContext? Context, EventResultStatus Refusal)> AtaContextAsync(MeetingAccess access, int id)
    {
        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        if (meeting.IsCancelled || meeting.Date.Date > Today)
        {
            return (null, EventResultStatus.Closed);
        }

        var existing = await _atas.GetByMeetingIdAsync(meeting.Id);
        return (new AtaContext(meeting, existing, CanWriteAta(access, meeting, existing)), EventResultStatus.Ok);
    }

    /// <summary>
    /// The ata "Ver Ata" may open: the meeting is before today, the member passes the reading rule, and the ata is
    /// published, or a draft they may write (decision A1). Null = nothing to show (a draft is never revealed).
    /// </summary>
    private MeetingAta? ReadableAta(MeetingAccess access, AtaContext context)
    {
        var ata = context.Existing;
        if (ata is null || context.Meeting.Date.Date >= Today || !access.CanReadPublishedAta(context.Meeting))
        {
            return null;
        }

        return ata.Status == MeetingAtaStatus.Published || context.CanWrite ? ata : null;
    }

    /// <summary>The ata-writing rule; roles are "Owner" or nothing (decision A2), an existing ata only from the day after.</summary>
    private bool CanWriteAta(MeetingAccess access, Meeting meeting, MeetingAta? existing) =>
        _atas.CanCreateOrEditAta(
            access.UserId,
            meeting,
            access.AtaRoles,
            access.User.Positions ?? new List<Position>(),
            meeting.Date.Date < Today ? existing : null);

    private async Task<(MeetingRequest? Request, EventResultStatus Refusal)> DecidableRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return (null, refusal);
        }

        if (!access.CanSeeRequests)
        {
            return (null, EventResultStatus.Forbidden);
        }

        var request = await _requests.GetByIdAsync(requestId);
        if (request is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        if (!access.CanDecide(request))
        {
            return (null, EventResultStatus.Forbidden);
        }

        return request.Status == RequestStatus.Pending ? (request, EventResultStatus.Ok) : (null, EventResultStatus.Closed);
    }

    private async Task<(MeetingRequest? Request, EventResultStatus Refusal)> OwnRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return (null, refusal);
        }

        if (!access.CanSeeRequests)
        {
            return (null, EventResultStatus.Forbidden);
        }

        var request = await _requests.GetByIdAsync(requestId);
        if (request is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        return access.OwnsRequest(request) ? (request, EventResultStatus.Ok) : (null, EventResultStatus.Forbidden);
    }
}
