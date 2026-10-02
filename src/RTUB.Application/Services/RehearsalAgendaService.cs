using System.Globalization;
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
/// The old Blazor /rehearsals for members, behind the React pages (React track 014, docs/react-rehearsals.md).
/// Reads Rehearsals and RehearsalAttendances; no schema change. Rules, as the old page:
/// - a rehearsal is past once its day is before today; presenças can be approved once it is past, or today
///   when it starts at 21:00 or later (every real rehearsal starts at 21:30);
/// - a member answers Vou / Não vou (instrument and note) on an upcoming, not cancelled rehearsal; a new "vou" is
///   pending until Admin/Owner approves it; a member may remove their own presença;
/// - instruments offered: the member's own, primary first; a Leitão with none may pick any.
/// </summary>
public sealed class RehearsalAgendaService : IRehearsalAgendaService
{
    public const int NotesMax = 500;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IRehearsalAttendanceService _attendance;

    public RehearsalAgendaService(IDbContextFactory<ApplicationDbContext> contexts, IRehearsalAttendanceService attendance)
    {
        _contexts = contexts;
        _attendance = attendance;
    }

    public async Task<EventResult<RehearsalAgendaDto>> GetAgendaAsync(ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
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
            mine.GetValueOrDefault(r.Id));

        return EventResult<RehearsalAgendaDto>.Ok(new RehearsalAgendaDto(
            manage,
            rehearsals.Where(r => !IsPast(r)).OrderBy(r => r.Date).ThenBy(r => r.Id).Select(Summary).ToList(),
            rehearsals.Where(IsPast).OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).Select(Summary).ToList()));
    }

    public async Task<EventResult<RehearsalDetailDto>> GetAsync(int id, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
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
        var rows = await db.RehearsalAttendances.AsNoTracking()
            .Where(a => a.RehearsalId == id && a.User != null)
            .Include(a => a.User)
            .OrderByDescending(a => a.CheckedInAt).ThenByDescending(a => a.Id)
            .ToListAsync();

        // The old list: Admin approves or removes once the rehearsal can be approved; anyone removes their own.
        var approvable = IsApprovable(r);
        RehearsalAttendeeDto Row(RehearsalAttendance a) => new(
            a.Id,
            Person(a),
            Status(a),
            a.WillAttend && a.Instrument is { } i ? StatusHelper.GetInstrumentDisplay(i) : null,
            a.WillAttend ? Blank(a.OtherInstruments) : null,
            Blank(a.Notes),
            a.UserId == me,
            manage && approvable && a.WillAttend && !a.Attended,
            (manage && approvable) || a.UserId == me);

        var going = rows.Where(a => a.WillAttend).ToList();
        var list = new RehearsalAttendanceListDto(
            going.Where(a => a.EffectiveCategory() != MemberCategory.Leitao).Select(Row).ToList(),
            going.Where(a => a.EffectiveCategory() == MemberCategory.Leitao).Select(Row).ToList(),
            rows.Where(a => !a.WillAttend).Select(Row).ToList());

        // As the old counters: what the going members play, then the other instruments they listed.
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
            ToSummary(r, going.Count, manage ? going.Count(a => !a.Attended) : 0, mine),
            Blank(r.Notes),
            manage,
            list,
            Counts(primary),
            Counts(others)));
    }

    public async Task<EventResult<RehearsalAttendanceDto>> GetMyAttendanceAsync(int id, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadAsync(id, me);
        return context is null ? EventResult<RehearsalAttendanceDto>.Fail(EventResultStatus.NotFound) : EventResult<RehearsalAttendanceDto>.Ok(context.ToDto());
    }

    public async Task<EventResult<RehearsalAttendanceDto>> SaveMyAttendanceAsync(int id, RehearsalAttendanceInput input, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
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

        // As the Blazor page did: the member's other instruments, by display name.
        var others = instrument is { } chosen
            ? string.Join(", ", context.Registered.Where(i => i != chosen).Select(StatusHelper.GetInstrumentDisplay))
            : null;
        await _attendance.MarkAttendanceAsync(id, me, input.WillAttend, instrument, notes, string.IsNullOrEmpty(others) ? null : others);
        return EventResult<RehearsalAttendanceDto>.Ok((await LoadAsync(id, me))!.ToDto());
    }

    public async Task<EventResult<RehearsalAttendanceDto>> RemoveMyAttendanceAsync(int id, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
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
        if (!RehearsalsAuthorization.IsMember(user))
        {
            return EventResult<RehearsalStatsDto>.Fail(EventResultStatus.SignInRequired);
        }

        // The old modal's rules: the current season by default; past, not cancelled rehearsals; "vou" rows only.
        var seasonStart = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var start = (from ?? new DateOnly(seasonStart, 9, 1)).ToDateTime(TimeOnly.MinValue);
        var end = (to ?? new DateOnly(seasonStart + 1, 8, 31)).ToDateTime(TimeOnly.MinValue);
        if (end < start)
        {
            return EventResult<RehearsalStatsDto>.Invalid("to", "A data de fim não pode ser anterior à de início.");
        }

        var endExclusive = end.AddDays(1);
        var today = DateTime.Today;
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

    // ---------- rules and mapping (shared with RehearsalAdminService) ----------

    internal static bool IsPast(Rehearsal r) => r.Date.Date < DateTime.Today;

    internal static bool IsApprovable(Rehearsal r) => IsPast(r) || (r.Date.Date == DateTime.Today && r.StartTime.Hours >= 21);

    internal static string Status(RehearsalAttendance a) => !a.WillAttend ? "notGoing" : a.Attended ? "approved" : "pending";

    internal static RehearsalSummaryDto ToSummary(Rehearsal r, int going, int pending, RehearsalAttendance? mine) => new(
        r.Id,
        DateText(r.Date),
        Time(r.StartTime),
        Time(r.EndTime),
        r.Location,
        Blank(r.Theme),
        Blank(r.Description),
        r.IsCanceled,
        r.IsCanceled ? Blank(r.CancellationReason) : null,
        IsPast(r),
        IsApprovable(r),
        Season(r.Date),
        going,
        pending,
        mine is null ? null : new RehearsalMineDto(Status(mine),
            mine.WillAttend && mine.Instrument is { } i ? StatusHelper.GetInstrumentDisplay(i) : null, Blank(mine.Notes)));

    /// <summary>Nickname first; Magister, else the category at the rehearsal (else the current one), as the badge.</summary>
    internal static EventAuthorDto Person(RehearsalAttendance a)
    {
        var u = a.User!;
        var who = EventDiscussionBoardService.Author(u.Nickname, u.FirstName, u.LastName, u.ImageUrl, null, null);
        var badge = u.Positions?.Contains(Position.Magister) == true ? "MAGISTER"
            : a.EffectiveCategory() is { } c ? StatusHelper.GetCategoryDisplay(c) : null;
        return who with { Badge = badge };
    }

    private static IReadOnlyList<RehearsalInstrumentCountDto> Counts(IEnumerable<(InstrumentType Instrument, int Count)> counts) =>
        counts.OrderByDescending(c => c.Count).ThenBy(c => c.Instrument)
            .Select(c => new RehearsalInstrumentCountDto(StatusHelper.GetInstrumentDisplay(c.Instrument), c.Count)).ToList();

    internal static string DateText(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Time(TimeSpan t) => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    internal static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>September to August, as the old fiscal-year filter.</summary>
    private static string Season(DateTime date)
    {
        var start = date.Month >= 9 ? date.Year : date.Year - 1;
        return $"{start}-{start + 1}";
    }

    // ---------- the caller's presença ----------

    private sealed record Context(
        RehearsalSummaryDto Rehearsal,
        string State,
        RehearsalAttendance? Attendance,
        bool IsLeitao,
        IReadOnlyList<InstrumentType> Registered,
        IReadOnlyList<InstrumentType> Options)
    {
        public RehearsalAttendanceDto ToDto() => new(
            Rehearsal,
            State,
            Attendance is null ? null : Status(Attendance),
            Attendance is { WillAttend: true, Instrument: { } i } ? i.ToString() : null,
            Attendance?.Notes,
            IsLeitao,
            Options.Select(i => new EventInstrumentOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i))).ToList(),
            Registered.Count > 0 ? Registered[0].ToString() : null);
    }

    private async Task<Context?> LoadAsync(int id, string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var r = await db.Rehearsals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null)
        {
            return null;
        }

        var attendance = await db.RehearsalAttendances.AsNoTracking().FirstOrDefaultAsync(a => a.RehearsalId == id && a.UserId == userId);
        var going = await db.RehearsalAttendances.CountAsync(a => a.RehearsalId == id && a.WillAttend);
        var categories = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Categories).FirstOrDefaultAsync();
        var isLeitao = categories?.Contains(MemberCategory.Leitao) == true;
        var registered = await db.MemberInstruments.AsNoTracking()
            .Where(i => i.MemberId == userId)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
            .Select(i => i.InstrumentType)
            .ToListAsync();
        var options = registered.Count > 0 ? registered : isLeitao ? Enum.GetValues<InstrumentType>().ToList() : new List<InstrumentType>();
        var state = r.IsCanceled ? "cancelled" : IsPast(r) ? "past" : "open";
        return new Context(ToSummary(r, going, 0, attendance), state, attendance, isLeitao, registered, options);
    }
}
