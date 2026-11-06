using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// "As minhas ações pendentes" (task 075): the presidents' meetings and atas, and the Ensaiador / Magister's presenças and
// repertoire. Shown by the current position (ApplicationUser.Positions); the writes are the modules' own, which check
// the same positions (MeetingAccess, RehearsalsAuthorization.CanApprove, EventRepertoireAdminService).

/// <summary>
/// Reuniões (current Presidente da Mesa, Magister - the Direção's president - and Presidente do CV): for each body the
/// member presides over, its past, not cancelled meetings of the current fiscal year with no ata saved (required
/// "Criar ata": a saved draft resolves it, publishing is separate; older gaps belong to earlier mandates), then an
/// optional monthly "Registar reunião" (a meeting the app cannot know about; "Agora não" for this month). Someone presiding over several bodies gets the union.
/// </summary>
public sealed class MeetingsPendingActionSource : IPendingActionSource
{
    public const string KindCode = "meetings";

    public string Panel => "meetings";
    public string Title => "Reuniões";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    private static readonly MeetingType[] Bodies = [MeetingType.AssembleiaGeralOrdinaria, MeetingType.ReuniaoDirecao, MeetingType.ConselhoVeteranos];

    public bool AppliesTo(PendingActionMember member) => Bodies.Any(b => member.Holds(MeetingAccess.PresidentOf(b)));

    public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) =>
        await Missing(db, member).CountAsync() + (await RegisterRowsAsync(db, member)).Count;

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var missing = await Missing(db, member).CountAsync();
        var rows = new List<PendingActionRowDto>();
        if (skip < missing)
        {
            var meetings = await Missing(db, member).OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).Skip(skip).Take(take)
                .Select(m => new { m.Id, m.Title, m.Date, m.Type, m.Location }).ToListAsync();
            rows.AddRange(meetings.Select(m => AtaRow(m.Id, m.Title, m.Date, m.Type, m.Location)));
        }

        var register = await RegisterRowsAsync(db, member);
        rows.AddRange(register.Skip(Math.Max(0, skip - missing)).Take(take - rows.Count));
        return rows;
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind)
        {
            return null;
        }

        if (sourceKey.StartsWith("ata:", StringComparison.Ordinal) && AttendanceFilters.Id(sourceKey[4..]) is { } id)
        {
            var m = await Missing(db, member).Where(x => x.Id == id).Select(x => new { x.Id, x.Title, x.Date, x.Type, x.Location }).FirstOrDefaultAsync();
            return m is null ? null : (AtaRow(m.Id, m.Title, m.Date, m.Type, m.Location), "");
        }

        var row = (await RegisterRowsAsync(db, member)).FirstOrDefault(r => r.SourceKey == sourceKey);
        return row is null ? null : (row, Month(member));
    }

    private string Kind => KindCode;

    /// <summary>The member's bodies' types (AG covers AG and AGE).</summary>
    private static List<MeetingType> Types(PendingActionMember member) =>
        Enum.GetValues<MeetingType>().Where(t => member.Holds(MeetingAccess.PresidentOf(t))).ToList();

    private static IQueryable<Meeting> Missing(ApplicationDbContext db, PendingActionMember member)
    {
        var types = Types(member);
        var (from, today) = (member.FiscalYearStartDate, member.Today);
        // The current fiscal year only: the mandate in office answers for its own meetings, not every older gap.
        return db.Meetings.AsNoTracking()
            .Where(m => types.Contains(m.Type) && !m.IsCancelled && m.Date >= from && m.Date < today && !db.MeetingAtas.Any(a => a.MeetingId == m.Id));
    }

    private static string Month(PendingActionMember member) => member.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private async Task<List<PendingActionRowDto>> RegisterRowsAsync(ApplicationDbContext db, PendingActionMember member)
    {
        var month = Month(member);
        var dismissed = await db.PendingActionDismissals.AsNoTracking()
            .Where(d => d.UserId == member.UserId && d.ActionKind == KindCode && d.Period == month)
            .Select(d => d.SourceKey).ToListAsync();
        return Bodies.Where(b => member.Holds(MeetingAccess.PresidentOf(b)))
            .Select(b => $"register:{b}")
            .Where(key => !dismissed.Contains(key))
            .Select(key => RegisterRow(Enum.Parse<MeetingType>(key["register:".Length..])))
            .ToList();
    }

    internal static string BodyName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.ReuniaoDirecao => "Direção",
        _ => "Assembleia Geral",
    };

    private static string TypeName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
        MeetingType.ReuniaoDirecao => "Reunião de Direção",
        _ => "Reunião",
    };

    private static PendingActionRowDto AtaRow(int id, string title, DateTime date, MeetingType type, string? location) => new(
        KindCode, $"ata:{id.ToString(CultureInfo.InvariantCulture)}", title, AttendanceFilters.Day(date), AttendanceFilters.Clock(date.TimeOfDay),
        AttendanceFilters.Join(TypeName(type), location, "Sem ata"),
        Required: true, Dismissible: false,
        [new PendingActionButtonDto("Criar ata", $"/meetings?meeting={id}&ata=1", true)],
        $"/meetings?meeting={id}");

    private static PendingActionRowDto RegisterRow(MeetingType body) => new(
        KindCode, $"register:{body}", $"Registar reunião · {BodyName(body)}", null, null,
        "Houve ou vai haver uma reunião que ainda não está na app? Regista-a (uma reunião passada não é anunciada).",
        Required: false, Dismissible: true,
        [new PendingActionButtonDto("Registar reunião", $"/meetings?new={body}", true)]);
}

/// <summary>
/// Presenças por aprovar (current Ensaiador / Magister): every pending presença ("vou" / "fui" not yet confirmed) at a
/// rehearsal of the current fiscal year that can be approved (past, or today from 21:00) and is not cancelled (older
/// ones stay approvable on their rehearsal page). Aprovar / Recusar are the rehearsal
/// page's own (<see cref="IRehearsalAdminService"/>): approval confirms (member push, activity status, XP); refusal records
/// "não foi" and notifies the member, so the claim never comes back as unanswered.
/// </summary>
public sealed class RehearsalApprovalsPendingActionSource : IPendingActionSource, IPendingActionCommand
{
    public const string KindCode = "rehearsal.approval";

    private static readonly IReadOnlySet<string> Commands = new HashSet<string> { "approve", "refuse" };
    private static readonly TimeSpan Evening = new(21, 0, 0);
    private readonly IRehearsalAdminService _rehearsals;

    public RehearsalApprovalsPendingActionSource(IRehearsalAdminService rehearsals)
    {
        _rehearsals = rehearsals;
    }

    public string Kind => KindCode;
    public IReadOnlySet<string> Answers => Commands;
    public string Panel => "approvals";
    public string Title => "Presenças por aprovar";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => member.Holds(RehearsalsAuthorization.Approvers);

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Pending(db, member).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var rows = await Pending(db, member)
            .OrderByDescending(a => a.Rehearsal!.Date).ThenBy(a => a.CheckedInAt).ThenBy(a => a.Id)
            .Skip(skip).Take(take)
            .Select(Project)
            .ToListAsync();
        return rows.Select(Row).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == Kind && AttendanceFilters.Id(sourceKey) is { } id
            && await Pending(db, member).Where(a => a.Id == id).Select(Project).FirstOrDefaultAsync() is { } item
            ? (Row(item), "")
            : null;

    public async Task<EventResult<bool>> RunAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, string answer)
    {
        if (AttendanceFilters.Id(sourceKey) is not { } id
            || await Pending(db, member).Where(a => a.Id == id).Select(a => (int?)a.RehearsalId).FirstOrDefaultAsync() is not { } rehearsalId)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var result = answer == "approve"
            ? await _rehearsals.ApproveAsync(rehearsalId, id, member.ToPrincipal())
            : await _rehearsals.RefuseAsync(rehearsalId, id, member.ToPrincipal());
        // Decided meanwhile (approved, refused, removed): for the page it is simply no longer pending.
        return result.Status == EventResultStatus.Closed ? EventResult<bool>.Fail(EventResultStatus.NotFound) : TaskRows.From(result);
    }

    /// <summary>
    /// Pending presenças at approvable rehearsals (<see cref="RehearsalAgendaService.IsApprovable"/>: past, or today starting
    /// at 21:00 or later). SQLite cannot compare a TimeSpan, so today's approvable rehearsals (one or two rows) are decided
    /// in memory first and the rest is SQL.
    /// </summary>
    private static IQueryable<RehearsalAttendance> Pending(ApplicationDbContext db, PendingActionMember member)
    {
        var today = member.Today;
        var tonight = db.Rehearsals.AsNoTracking().Where(r => r.Date == today).Select(r => new { r.Id, r.StartTime }).AsEnumerable()
            .Where(r => r.StartTime >= Evening).Select(r => r.Id).ToList();
        var from = member.FiscalYearStartDate;
        return db.RehearsalAttendances.AsNoTracking()
            .Where(a => a.WillAttend && !a.Attended && a.User != null && !a.Rehearsal!.IsCanceled && a.Rehearsal.Date >= from
                        && (a.Rehearsal.Date < today || tonight.Contains(a.RehearsalId)));
    }

    private sealed record Item(int Id, int RehearsalId, DateTime Date, TimeSpan Start, string? Theme, string? Nickname, string? FirstName,
        string? LastName, InstrumentType? Instrument, string? Notes);

    private static readonly System.Linq.Expressions.Expression<Func<RehearsalAttendance, Item>> Project = a => new Item(
        a.Id, a.RehearsalId, a.Rehearsal!.Date, a.Rehearsal.StartTime, a.Rehearsal.Theme,
        a.User!.Nickname, a.User.FirstName, a.User.LastName, a.Instrument, a.Notes);

    private static PendingActionRowDto Row(Item i)
    {
        var who = GovernanceService.ToMember(i.Nickname, i.FirstName, i.LastName, null).DisplayName;
        var day = i.Date.ToString("dd/MM", CultureInfo.InvariantCulture);
        return new PendingActionRowDto(
            KindCode, i.Id.ToString(CultureInfo.InvariantCulture), who, AttendanceFilters.Day(i.Date), AttendanceFilters.Clock(i.Start),
            AttendanceFilters.Join(string.IsNullOrWhiteSpace(i.Theme) ? "Ensaio" : $"Ensaio · {i.Theme!.Trim()}",
                i.Instrument is { } instrument ? StatusHelper.GetInstrumentDisplay(instrument) : null,
                string.IsNullOrWhiteSpace(i.Notes) ? null : $"“{i.Notes!.Trim()}”"),
            Required: false, Dismissible: false,
            [
                new PendingActionButtonDto("Aprovar", null, true, "approve"),
                new PendingActionButtonDto("Recusar", null, false, "refuse",
                    $"Recusar a presença de {who} no ensaio de {day}? Fica registado que não foi e {who} recebe uma notificação.",
                    "Recusar presença"),
            ],
            $"/rehearsals/{i.RehearsalId}");
    }
}

/// <summary>
/// Repertório (current Ensaiador / Magister): upcoming, not cancelled events with no song in their repertoire.
/// "Preparar repertório" opens the event's repertoire editor (the same editor; the Ensaiador / Magister may use it for
/// upcoming events, task 075). "Não é necessário" is the member's own dismissal of that event; a repertoire saved by
/// anyone resolves it for everyone.
/// </summary>
public sealed class RepertoirePendingActionSource : IPendingActionSource
{
    public const string KindCode = "repertoire.event";

    public string Panel => "repertoire";
    public string Title => "Repertório";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => member.Holds(RehearsalsAuthorization.Approvers);

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Query(db, member).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var events = await Query(db, member).OrderBy(e => e.Date).ThenBy(e => e.Id).Skip(skip).Take(take)
            .Select(e => new { e.Id, e.Name, e.Date, e.Location }).ToListAsync();
        return events.Select(e => Row(e.Id, e.Name, e.Date, e.Location)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == KindCode && AttendanceFilters.Id(sourceKey) is { } id
            && await Query(db, member).Where(e => e.Id == id).Select(e => new { e.Id, e.Name, e.Date, e.Location }).FirstOrDefaultAsync() is { } ev
            ? (Row(ev.Id, ev.Name, ev.Date, ev.Location), "")
            : null;

    private static IQueryable<Event> Query(ApplicationDbContext db, PendingActionMember member)
    {
        var today = member.Today;
        var dismissed = db.PendingActionDismissals.Where(d => d.UserId == member.UserId && d.ActionKind == KindCode && d.Period == "")
            .Select(d => d.SourceKey);
        return db.Events.AsNoTracking()
            .Where(e => !e.IsCancelled && (e.EndDate ?? e.Date) >= today && !db.EventRepertoires.Any(r => r.EventId == e.Id))
            .Where(e => !dismissed.Contains(e.Id.ToString()));
    }

    private static PendingActionRowDto Row(int id, string name, DateTime date, string? location) => new(
        KindCode, id.ToString(CultureInfo.InvariantCulture), name, AttendanceFilters.Day(date), AttendanceFilters.Clock(date.TimeOfDay),
        AttendanceFilters.Join(location, "Sem repertório"),
        Required: false, Dismissible: true,
        [new PendingActionButtonDto("Preparar repertório", $"/events/{id}?repertoire=1", true)],
        $"/events/{id}",
        DismissLabel: "Não é necessário");
}
