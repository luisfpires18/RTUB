// Rehearsal admin: attendance approval and manual attendees.
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class RehearsalAdminService
{
    public async Task<EventResult<bool>> ApproveAsync(int id, int attendanceId, ClaimsPrincipal user)
    {
        var (a, refused) = await PendingClaimAsync(id, attendanceId, user);
        if (a is null)
        {
            return refused!;
        }

        await _attendance.UpdateAttendanceAsync(a.Id, true, a.Instrument, RehearsalsAuthorization.UserId(user));
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// Task 075: "Recusar" a pending presença. Durable, not a deletion (which would let the member's "Fui / Não fui" prompt
    /// come back): <see cref="IRehearsalAttendanceService.CancelAttendanceAsync"/> records it as not attended
    /// (<c>WillAttend = false</c>, audited with the presença) and sends the member the "Presença recusada" push.
    /// </summary>
    public async Task<EventResult<bool>> RefuseAsync(int id, int attendanceId, ClaimsPrincipal user)
    {
        var (a, refused) = await PendingClaimAsync(id, attendanceId, user);
        if (a is null)
        {
            return refused!;
        }

        await _attendance.CancelAttendanceAsync(a.Id, RehearsalsAuthorization.UserId(user));
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// A presença an approver may decide: the caller is Admin / Owner or the current Ensaiador / Magister (never their own
    /// presença unless Admin / Owner); the rehearsal can be approved (past, or today from 21:00) and the presença is still pending ("vou" / "fui", not confirmed).
    /// </summary>
    private async Task<(RehearsalAttendance? Attendance, EventResult<bool>? Refusal)> PendingClaimAsync(int id, int attendanceId, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
        {
            return (null, EventResult<bool>.Fail(EventResultStatus.SignInRequired));
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            var positions = await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.Positions).FirstOrDefaultAsync();
            if (!RehearsalsAuthorization.CanApprove(user, positions))
            {
                return (null, EventResult<bool>.Fail(EventResultStatus.Forbidden));
            }
        }

        var (r, a) = await AttendanceAsync(id, attendanceId);
        if (a is null)
        {
            return (null, EventResult<bool>.Fail(EventResultStatus.NotFound));
        }

        // A position holder never decides their own presença (no self-granted XP / activity); Admin / Owner as before.
        if (a.UserId == me && !RehearsalsAuthorization.CanManage(user))
        {
            return (null, EventResult<bool>.Fail(EventResultStatus.Forbidden));
        }

        return !RehearsalAgendaService.IsApprovable(r!, Today) || !a.WillAttend || a.Attended
            ? (null, EventResult<bool>.Fail(EventResultStatus.Closed))
            : (a, null);
    }

    public async Task<EventResult<bool>> RemoveAttendanceAsync(int id, int attendanceId, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        var (r, a) = await AttendanceAsync(id, attendanceId);
        if (a is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (a.UserId != me && !(RehearsalsAuthorization.CanManage(user) && RehearsalAgendaService.IsApprovable(r!, Today)))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _attendance.DeleteAttendanceAsync(a.Id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchMembersAsync(int id, string? query, ClaimsPrincipal user)
    {
        if (await RefusalAsync<IReadOnlyList<EventMemberOptionDto>>(id, user) is { } refused)
        {
            return refused;
        }

        var q = EventParticipantsAdminService.Fold(query);
        if (q.Length == 0)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(Array.Empty<EventMemberOptionDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var answered = db.RehearsalAttendances.Where(a => a.RehearsalId == id).Select(a => a.UserId);
        // ponytail: every member in memory (~100) to fold accents the Portuguese way; page it if it grows.
        var members = await db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled && !answered.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(members
            .Select(u => (u, who: EventDiscussionBoardService.Author(u.Nickname, u.FirstName, u.LastName, u.ImageUrl, null, null)))
            .Where(x => EventParticipantsAdminService.Fold(x.u.Nickname).Contains(q)
                        || EventParticipantsAdminService.Fold($"{x.u.FirstName} {x.u.LastName}").Contains(q))
            .OrderBy(x => x.who.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new EventMemberOptionDto(x.u.Id, x.who.Name, x.who.FullName, x.who.AvatarUrl))
            .ToList());
    }

    public async Task<EventResult<bool>> AddAttendeeAsync(int id, RehearsalAttendeeInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        var r = await FindAsync(id);
        if (r!.IsCanceled || !RehearsalAgendaService.IsApprovable(r, Today))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (string.IsNullOrWhiteSpace(input.UserId) || !await db.Users.AnyAsync(u => u.Id == input.UserId && !u.IsExpelled))
            {
                return EventResult<bool>.Invalid("userId", "Escolha um membro da lista.");
            }

            if (await db.RehearsalAttendances.AnyAsync(a => a.RehearsalId == id && a.UserId == input.UserId))
            {
                return EventResult<bool>.Invalid("userId", "Este membro já respondeu a este ensaio.");
            }
        }

        var primary = await _instruments.GetPrimaryInstrumentAsync(input.UserId);
        await _attendance.CreateAttendanceWithApprovalAsync(id, input.UserId, primary?.InstrumentType);
        return EventResult<bool>.Ok(true);
    }
}
