using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Pending action: rehearsal presenças awaiting approval.

/// <summary>
/// Presenças por aprovar (current Ensaiador / Magister): every pending presença ("vou" / "fui" not yet confirmed) at a
/// rehearsal of the current fiscal year that can be approved (past, or today from 21:00) and is not cancelled (older
/// ones stay approvable on their rehearsal page). Never the member's own presença: nobody approves themselves. Aprovar / Recusar are the rehearsal
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
            .Where(a => a.WillAttend && !a.Attended && a.User != null && a.UserId != member.UserId && !a.Rehearsal!.IsCanceled && a.Rehearsal.Date >= from
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
