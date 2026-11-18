using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The member side of the React /rehearsals pages (docs/react-rehearsals.md).
/// Reads Rehearsals and RehearsalAttendances; no schema change. Rules:
/// - a rehearsal is past once its day is before today; presenças can be approved once it is past, or today
///   when it starts at 21:00 or later (every real rehearsal starts at 21:30);
/// - a member answers Vou / Não vou (instrument and note) on an upcoming, not cancelled rehearsal; a new "vou" is
///   pending until Admin/Owner approves it; a member may remove their own presença;
/// - instruments offered: the member's own, primary first; a Leitão with none may pick any.
/// </summary>
public sealed partial class RehearsalAgendaService : IRehearsalAgendaService
{
    private readonly TimeProvider _time;

    public const int NotesMax = 500;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    private readonly IRehearsalAttendanceService _attendance;

    public RehearsalAgendaService(IDbContextFactory<ApplicationDbContext> contexts, IRehearsalAttendanceService attendance, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _attendance = attendance;
    }

    public async Task<EventResult<RehearsalAgendaDto>> GetAgendaAsync(ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<RehearsalAgendaDto>.Fail(EventResultStatus.SignInRequired);
        }

        var manage = RehearsalsAuthorization.CanManage(user);
        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: every rehearsal in memory (74 today, ~70 a season); page by season if it reaches thousands.
        var rehearsals = await db.Rehearsals.AsNoTracking().ToListAsync();
        var counts = await db.RehearsalAttendances.AsNoTracking()
            .GroupBy(a => a.RehearsalId)
            .Select(g => new { Id = g.Key, Going = g.Count(a => a.WillAttend), Pending = g.Count(a => a.WillAttend && !a.Attended) })
            .ToDictionaryAsync(x => x.Id);
        var mine = await db.RehearsalAttendances.AsNoTracking().Where(a => a.UserId == me).ToDictionaryAsync(a => a.RehearsalId);

        RehearsalSummaryDto Summary(Rehearsal r) => ToSummary(r,
            counts.TryGetValue(r.Id, out var c) ? c.Going : 0,
            manage && counts.TryGetValue(r.Id, out var p) ? p.Pending : 0,
            mine.GetValueOrDefault(r.Id), Today);

        return EventResult<RehearsalAgendaDto>.Ok(new RehearsalAgendaDto(
            manage,
            rehearsals.Where(r => !IsPast(r, Today)).OrderBy(r => r.Date).ThenBy(r => r.Id).Select(Summary).ToList(),
            rehearsals.Where(r => IsPast(r, Today)).OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).Select(Summary).ToList(),
            RehearsalsAuthorization.CanEdit(user)));
    }

    public async Task<EventResult<RehearsalDetailDto>> GetAsync(int id, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<RehearsalDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var r = await db.Rehearsals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null)
        {
            return EventResult<RehearsalDetailDto>.Fail(EventResultStatus.NotFound);
        }

        var manage = RehearsalsAuthorization.CanManage(user);
        // The current Ensaiador / Magister approve and refuse too (nothing else of management).
        var approver = RehearsalsAuthorization.CanApprove(user,
            await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.Positions).FirstOrDefaultAsync());
        var rows = await db.RehearsalAttendances.AsNoTracking()
            .Where(a => a.RehearsalId == id && a.User != null)
            .Include(a => a.User)
            .OrderByDescending(a => a.CheckedInAt).ThenByDescending(a => a.Id)
            .ToListAsync();

        // Admin approves or removes once the rehearsal can be approved; anyone removes their own.
        var approvable = IsApprovable(r, Today);
        RehearsalAttendeeDto Row(RehearsalAttendance a) => new(
            a.Id,
            Person(a),
            Status(a),
            a.WillAttend && a.Instrument is { } i ? StatusHelper.GetInstrumentDisplay(i) : null,
            a.WillAttend ? Blank(a.OtherInstruments) : null,
            Blank(a.Notes),
            a.UserId == me,
            approver && approvable && a.WillAttend && !a.Attended && (manage || a.UserId != me),
            (manage && approvable) || a.UserId == me);

        var going = rows.Where(a => a.WillAttend).ToList();
        var list = new RehearsalAttendanceListDto(
            going.Where(a => a.EffectiveCategory() != MemberCategory.Leitao).Select(Row).ToList(),
            going.Where(a => a.EffectiveCategory() == MemberCategory.Leitao).Select(Row).ToList(),
            rows.Where(a => !a.WillAttend).Select(Row).ToList());

        // What the going members play, then the other instruments they listed.
        var primary = going.Where(a => a.Instrument.HasValue).GroupBy(a => a.Instrument!.Value)
            .Select(g => (g.Key, g.Count()));
        var others = going.SelectMany(a => (a.OtherInstruments ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(InstrumentTypeHelper.ParseDisplayName))
            .Where(i => i.HasValue).GroupBy(i => i!.Value)
            .Select(g => (g.Key, g.Count()));

        var mine = rows.FirstOrDefault(a => a.UserId == me)
                   ?? await db.RehearsalAttendances.AsNoTracking().FirstOrDefaultAsync(a => a.RehearsalId == id && a.UserId == me);
        return EventResult<RehearsalDetailDto>.Ok(new RehearsalDetailDto(
            ToSummary(r, going.Count, manage ? going.Count(a => !a.Attended) : 0, mine, Today),
            Blank(r.Notes),
            manage,
            list,
            Counts(primary),
            Counts(others),
            RehearsalsAuthorization.CanEdit(user)));
    }

    public async Task<EventResult<RehearsalAttendanceDto>> GetMyAttendanceAsync(int id, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadAsync(id, me);
        return context is null ? EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.NotFound) : EventResult<RehearsalAttendanceDto>.Ok(context.ToDto());
    }

    public async Task<EventResult<RehearsalAttendanceDto>> SaveMyAttendanceAsync(int id, RehearsalAttendanceInput input, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadAsync(id, me);
        if (context is null)
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.NotFound);
        }

        if (context.State != "open")
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.Closed);
        }

        var notes = Blank(input.Notes);
        if (notes?.Length > NotesMax)
        {
            return EventResult<RehearsalAttendanceDto>.Invalid("notes", $"A nota não pode exceder {NotesMax} caracteres.");
        }

        // Only someone who goes plays; the choice must be one the page offered.
        InstrumentType? instrument = null;
        if (input.WillAttend && !string.IsNullOrWhiteSpace(input.Instrument))
        {
            if (!Enum.TryParse<InstrumentType>(input.Instrument, out var offered) || !context.Options.Contains(offered))
            {
                return EventResult<RehearsalAttendanceDto>.Invalid("instrument", "Escolha um dos instrumentos indicados.");
            }

            instrument = offered;
        }

        // The member's other instruments, by display name.
        var others = instrument is { } chosen
            ? string.Join(", ", context.Registered.Where(i => i != chosen).Select(StatusHelper.GetInstrumentDisplay))
            : null;
        await _attendance.MarkAttendanceAsync(id, me, input.WillAttend, instrument, notes, string.IsNullOrEmpty(others) ? null : others);
        return EventResult<RehearsalAttendanceDto>.Ok((await LoadAsync(id, me))!.ToDto());
    }

    public async Task<EventResult<RehearsalAttendanceDto>> RemoveMyAttendanceAsync(int id, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadAsync(id, me);
        if (context?.Attendance is null)
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.NotFound);
        }

        await _attendance.DeleteAttendanceAsync(context.Attendance.Id);
        return EventResult<RehearsalAttendanceDto>.Ok((await LoadAsync(id, me))!.ToDto());
    }

    public async Task<EventResult<RehearsalStatsDto>> GetStatsAsync(DateOnly? from, DateOnly? to, ClaimsPrincipal user)
    {
        if (!user.IsMember())
        {
            return EventResult<RehearsalStatsDto>.Fail(EventResultStatus.SignInRequired);
        }

        // The current season by default; past, not cancelled rehearsals; "vou" rows only.
        var seasonStart = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var start = (from ?? new DateOnly(seasonStart, 9, 1)).ToDateTime(TimeOnly.MinValue);
        var end = (to ?? new DateOnly(seasonStart + 1, 8, 31)).ToDateTime(TimeOnly.MinValue);
        if (end < start)
        {
            return EventResult<RehearsalStatsDto>.Invalid("to", "A data de fim não pode ser anterior à de início.");
        }

        var endExclusive = end.AddDays(1);
        var today = Today;
        await using var db = await _contexts.CreateDbContextAsync();
        var past = await db.Rehearsals.CountAsync(r => r.Date >= start && r.Date < endExclusive && r.Date < today && !r.IsCanceled);
        var rows = await db.RehearsalAttendances.AsNoTracking()
            .Where(a => a.WillAttend && a.User != null && a.Rehearsal!.Date >= start && a.Rehearsal.Date < endExclusive)
            .Select(a => new
            {
                a.UserId,
                a.Attended,
                Counts = a.Rehearsal!.Date < today && !a.Rehearsal.IsCanceled,
                a.User!.Nickname,
                a.User.FirstName,
                a.User.LastName,
                a.User.ImageUrl,
                a.User.Categories,
            })
            .ToListAsync();

        var members = rows.GroupBy(r => r.UserId).Select(g =>
            {
                var r = g.First();
                var categories = r.Categories ?? new List<MemberCategory>();
                var who = EventDiscussionBoardService.Author(r.Nickname, r.FirstName, r.LastName, r.ImageUrl, null, null);
                var groups = new List<string>();
                if (categories.Any(c => c is MemberCategory.Tuno or MemberCategory.Veterano or MemberCategory.Tunossauro)) groups.Add("tuno");
                if (categories.Contains(MemberCategory.Caloiro)) groups.Add("caloiro");
                if (categories.Contains(MemberCategory.Leitao)) groups.Add("leitao");
                return new RehearsalMemberStatsDto(who.Name, who.FullName, who.AvatarUrl,
                    categories.Select(StatusHelper.GetCategoryDisplay).ToList(), groups,
                    g.Count(x => x.Counts && x.Attended), g.Count(x => x.Counts && !x.Attended));
            })
            .OrderByDescending(m => m.Approved).ThenByDescending(m => m.Pending).ThenBy(m => m.Name, StringComparer.CurrentCulture)
            .ToList();

        return EventResult<RehearsalStatsDto>.Ok(new RehearsalStatsDto(DateText(start), DateText(end), past, members));
    }
}
