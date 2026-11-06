using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// "As minhas ações pendentes" (task 074): responsibilities of the current Órgãos Sociais mandate. Who sees a panel is the
// position held now (ApplicationUser.Positions, synced to the mandate in office); a role alone shows nothing. What may be
// written is still the module's own rule, run as the member with the roles read from the database (holding the position
// grants Admin, or Mod for a Caloiro, on its own - RoleManagementService), so a treasurer whose role was taken away is
// refused honestly.

/// <summary>
/// Tesouraria (1.º / 2.º Tesoureiro): the current fiscal year's report (required while missing), then every event of the
/// year that has begun, is not cancelled and is not yet financially classified - an activity of that report linked to the
/// event (<c>Activity.EventId</c>), or the shared "Sem movimentos" (<c>Event.HasNoFinances</c>). Shared: whatever one
/// treasurer does, here or on the report page, resolves it for both.
/// </summary>
public sealed class TreasuryPendingActionSource : IPendingActionSource, IPendingActionCommand
{
    public const string KindCode = "treasury";
    private const string Open = "open";
    private const string None = "none";

    private static readonly IReadOnlySet<string> Commands = new HashSet<string> { "create", "none", "reopen" };
    private readonly ITreasuryService _treasury;

    public TreasuryPendingActionSource(ITreasuryService treasury)
    {
        _treasury = treasury;
    }

    public string Kind => KindCode;
    public IReadOnlySet<string> Answers => Commands;
    public string Panel => "treasury";
    public string Title => "Tesouraria";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [(Open, "Por classificar"), (None, "Sem movimentos")];

    public bool AppliesTo(PendingActionMember member) => member.Holds(Position.PrimeiroTesoureiro, Position.SegundoTesoureiro);

    public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter)
    {
        var report = await ReportIdAsync(db, member);
        return filter == None ? await Classified(db, member).CountAsync()
            : report is null ? 1
            : await Unclassified(db, member, report.Value).CountAsync();
    }

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var report = await ReportIdAsync(db, member);
        if (filter == None)
        {
            var decided = await Classified(db, member).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Skip(skip).Take(take)
                .Select(e => new Ev(e.Id, e.Name, e.Date, e.Location)).ToListAsync();
            return decided.Select(e => ReopenRow(e)).ToList();
        }

        if (report is null)
        {
            return [ReportRow(member)];
        }

        var open = await Unclassified(db, member, report.Value).OrderBy(e => e.Date).ThenBy(e => e.Id).Skip(skip).Take(take)
            .Select(e => new Ev(e.Id, e.Name, e.Date, e.Location)).ToListAsync();
        return open.Select(e => EventRow(e, report.Value)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind)
        {
            return null;
        }

        var report = await ReportIdAsync(db, member);
        if (sourceKey == ReportKey(member))
        {
            return report is null ? (ReportRow(member), member.FiscalYear) : null;
        }

        return EventId(sourceKey) is { } id && report is { } r
            && await Unclassified(db, member, r).Where(e => e.Id == id).Select(e => new Ev(e.Id, e.Name, e.Date, e.Location)).FirstOrDefaultAsync() is { } ev
            ? (EventRow(ev, r), member.FiscalYear)
            : null;
    }

    public async Task<EventResult<bool>> RunAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, string answer)
    {
        var report = await ReportIdAsync(db, member);
        if (answer == "create")
        {
            if (sourceKey != ReportKey(member) || report is not null)
            {
                return EventResult<bool>.Fail(EventResultStatus.NotFound);
            }

            // The report page's own creation (title, year rule, permission, one per year under its lock).
            var created = await _treasury.CreateReportAsync(new TreasuryReportInput(member.FiscalYearStart), member.ToPrincipal());
            return created.Status == EventResultStatus.Invalid && await ReportIdAsync(db, member) is not null
                ? EventResult<bool>.Fail(EventResultStatus.Conflict) // the other treasurer was faster
                : TaskRows.From(created);
        }

        if (EventId(sourceKey) is not { } id)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        // "Não" / "Reabrir" have no module page: the treasury's report rule decides who may record them.
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == member.UserId);
        if (!TreasuryAuthorization.CanManageReports(user, member.ToPrincipal()))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        var pending = answer == "none"
            ? report is { } r ? Unclassified(db, member, r) : null
            : answer == "reopen" ? Classified(db, member) : null;
        if (pending is null || !await pending.AnyAsync(e => e.Id == id))
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var ev = await db.Events.SingleAsync(e => e.Id == id);
        ev.HasNoFinances = answer == "none";
        await db.SaveChangesAsync(); // audited with the event
        return EventResult<bool>.Ok(true);
    }

    private static Task<int?> ReportIdAsync(ApplicationDbContext db, PendingActionMember member) =>
        db.Reports.AsNoTracking().Where(r => r.Year == member.FiscalYearStart).OrderBy(r => r.Id).Select(r => (int?)r.Id).FirstOrDefaultAsync();

    /// <summary>The fiscal year's events that have begun (start before tomorrow) and are not cancelled.</summary>
    private static IQueryable<RTUB.Core.Entities.Event> YearEvents(ApplicationDbContext db, PendingActionMember member)
    {
        var (from, tomorrow) = (member.FiscalYearStartDate, member.Today.AddDays(1));
        return db.Events.AsNoTracking().Where(e => !e.IsCancelled && e.Date >= from && e.Date < tomorrow);
    }

    private static IQueryable<RTUB.Core.Entities.Event> Unclassified(ApplicationDbContext db, PendingActionMember member, int reportId) =>
        YearEvents(db, member).Where(e => !e.HasNoFinances && !db.Activities.Any(a => a.ReportId == reportId && a.EventId == e.Id));

    private static IQueryable<RTUB.Core.Entities.Event> Classified(ApplicationDbContext db, PendingActionMember member) =>
        YearEvents(db, member).Where(e => e.HasNoFinances);

    private static string ReportKey(PendingActionMember member) => $"report:{member.FiscalYearStart}";

    private static int? EventId(string sourceKey) =>
        sourceKey.StartsWith("event:", StringComparison.Ordinal) ? AttendanceFilters.Id(sourceKey["event:".Length..]) : null;

    private sealed record Ev(int Id, string Name, DateTime Date, string? Location);

    private static PendingActionRowDto ReportRow(PendingActionMember member) => new(
        KindCode, ReportKey(member), $"Relatório de contas {member.FiscalYear}", null, null,
        "Ainda não existe o relatório deste ano letivo. Os eventos do ano aparecem depois de o criares.",
        Required: true, Dismissible: false,
        [new PendingActionButtonDto("Criar relatório", null, true, "create")], "/treasury");

    private static PendingActionRowDto EventRow(Ev e, int reportId) => new(
        KindCode, $"event:{e.Id.ToString(CultureInfo.InvariantCulture)}", e.Name, AttendanceFilters.Day(e.Date), AttendanceFilters.Clock(e.Date.TimeOfDay),
        AttendanceFilters.Join(e.Location, "Teve despesas ou receitas associadas?"),
        Required: false, Dismissible: false,
        [
            new PendingActionButtonDto("Sim", $"/treasury/reports/{reportId}?activityForEvent={e.Id}", true),
            new PendingActionButtonDto("Não", null, false, "none",
                $"Marcar «{e.Name}» como sem despesas nem receitas? Fica assim para os dois tesoureiros; podes reabrir em «Sem movimentos».",
                "Marcar sem movimentos"),
        ],
        $"/events/{e.Id}");

    private static PendingActionRowDto ReopenRow(Ev e) => new(
        KindCode, $"event:{e.Id.ToString(CultureInfo.InvariantCulture)}", e.Name, AttendanceFilters.Day(e.Date), null,
        AttendanceFilters.Join(e.Location, "Sem movimentos"),
        Required: false, Dismissible: false,
        [new PendingActionButtonDto("Reabrir", null, false, "reopen")],
        $"/events/{e.Id}");
}

/// <summary>
/// Secretaria (Secretário): the current fiscal year lacks folders the previous year had in Documentação (top-level
/// folders, the board files folder left out). The approved structure is last year's, as it is: nothing is invented.
/// "Criar pastas" creates only the missing ones through <see cref="IDocumentationService.CreateFolderAsync"/> (its name
/// rule, its permission; it refuses an existing name, so nothing is overwritten). Optional: "Agora não" is the
/// Secretário's own dismissal for this fiscal year.
/// </summary>
public sealed class SecretaryPendingActionSource : IPendingActionSource, IPendingActionCommand
{
    public const string KindCode = "secretary.folders";
    private const string LogisticsFolder = "Logistics";

    private static readonly IReadOnlySet<string> Commands = new HashSet<string> { "create" };
    private readonly IDocumentationService _documentation;

    public SecretaryPendingActionSource(IDocumentationService documentation)
    {
        _documentation = documentation;
    }

    public string Kind => KindCode;
    public IReadOnlySet<string> Answers => Commands;
    public string Panel => "secretary";
    public string Title => "Secretaria";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => member.Holds(Position.Secretario);

    public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) =>
        await PendingAsync(db, member) is null ? 0 : 1;

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take) =>
        skip == 0 && await PendingAsync(db, member) is { } missing ? [Row(member, missing)] : [];

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == Kind && sourceKey == "" && await PendingAsync(db, member) is { } missing ? (Row(member, missing), member.FiscalYear) : null;

    public async Task<EventResult<bool>> RunAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, string answer)
    {
        if (sourceKey != "" || await PendingAsync(db, member) is not { } missing)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        foreach (var name in missing.Folders)
        {
            var created = await _documentation.CreateFolderAsync(new DocumentFolderInput(name), member.ToPrincipal());
            // "Já existe" (made meanwhile, here or on the page) is fine; anything else stops and is reported.
            if (created.Status is not (EventResultStatus.Ok or EventResultStatus.Invalid))
            {
                return TaskRows.From(created);
            }
        }

        return EventResult<bool>.Ok(true);
    }

    private sealed record Missing(string Previous, IReadOnlyList<string> Folders);

    /// <summary>The folders to create, or null when none is missing, last year has none, or the Secretário said "agora não".</summary>
    private async Task<Missing?> PendingAsync(ApplicationDbContext db, PendingActionMember member)
    {
        if (await db.PendingActionDismissals.AnyAsync(d =>
                d.UserId == member.UserId && d.ActionKind == KindCode && d.SourceKey == "" && d.Period == member.FiscalYear))
        {
            return null;
        }

        var principal = member.ToPrincipal();
        var previous = $"{member.FiscalYearStart - 1}-{member.FiscalYearStart}";
        var before = await _documentation.GetAsync(previous, principal);
        var now = await _documentation.GetAsync(null, principal);
        if (before.Status != EventResultStatus.Ok || now.Status != EventResultStatus.Ok)
        {
            return null;
        }

        var existing = TopLevel(now.Value!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = TopLevel(before.Value!).Where(f => !existing.Contains(f)).Order(StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true)).ToList();
        return missing.Count == 0 ? null : new Missing(previous, missing);
    }

    private static IEnumerable<string> TopLevel(DocumentationDto year) =>
        year.Folders.Select(f => f.Name).Where(n => !n.Contains('/') && !n.Equals(LogisticsFolder, StringComparison.OrdinalIgnoreCase));

    private static PendingActionRowDto Row(PendingActionMember member, Missing missing)
    {
        var list = string.Join(", ", missing.Folders);
        return new PendingActionRowDto(
            KindCode, "", $"Pastas da documentação {member.FiscalYear}", null, null,
            $"Em falta, como em {missing.Previous}: {list}",
            Required: false, Dismissible: true,
            [
                new PendingActionButtonDto("Criar pastas", null, true, "create",
                    $"Criar {missing.Folders.Count} {(missing.Folders.Count == 1 ? "pasta" : "pastas")} em {member.FiscalYear}: {list}? " +
                    "Nenhuma pasta ou ficheiro existente é alterado."),
                new PendingActionButtonDto("Abrir", "/documentation", false),
            ],
            "/documentation");
    }
}
