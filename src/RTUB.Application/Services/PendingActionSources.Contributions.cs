using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// "As minhas ações pendentes" (task 077): optional contributions. Both prompts are personal: "Não tenho" / "Agora não"
// hides the row for the member only; the source state (a photo of the event, a post written this week) resolves it.

/// <summary>
/// Galeria (active members): events of the current fiscal year that are over, not cancelled, and have no photo
/// (<see cref="GalleryMedia.EventId"/> = the event, <see cref="MediaType.Image"/>; a video does not count). Members see
/// every event, so nothing is filtered by <c>Event.IsPublic</c>. "Adicionar" opens the gallery upload with the event
/// named; a photo uploaded for the event by anyone resolves it for everyone, and deleting that photo brings it back.
/// "Não tenho" is the member's own dismissal of that event for the fiscal year.
/// </summary>
public sealed class GalleryPendingActionSource : IPendingActionSource
{
    public const string KindCode = "gallery.event";

    public string Panel => "gallery";
    public string Title => "Galeria";
    public string Category => "participation";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => member.IsActive;

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Query(db, member).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var events = await Query(db, member).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Skip(skip).Take(take)
            .Select(e => new { e.Id, e.Name, e.Date, e.Location }).ToListAsync();
        return events.Select(e => Row(e.Id, e.Name, e.Date, e.Location)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == KindCode && AttendanceFilters.Id(sourceKey) is { } id
            && await Query(db, member).Where(e => e.Id == id).Select(e => new { e.Id, e.Name, e.Date, e.Location }).FirstOrDefaultAsync() is { } ev
            ? (Row(ev.Id, ev.Name, ev.Date, ev.Location), member.FiscalYear)
            : null;

    private static IQueryable<Event> Query(ApplicationDbContext db, PendingActionMember member)
    {
        var (from, today, period) = (member.FiscalYearStartDate, member.Today, member.FiscalYear);
        var dismissed = db.PendingActionDismissals.Where(d => d.UserId == member.UserId && d.ActionKind == KindCode && d.Period == period)
            .Select(d => d.SourceKey);
        return db.Events.AsNoTracking()
            .Where(e => !e.IsCancelled && e.Date >= from && (e.EndDate ?? e.Date) < today)
            .Where(e => !db.GalleryMedia.Any(m => m.EventId == e.Id && m.MediaType == MediaType.Image))
            .Where(e => !dismissed.Contains(e.Id.ToString()));
    }

    private static PendingActionRowDto Row(int id, string name, DateTime date, string? location)
    {
        var day = AttendanceFilters.Day(date);
        return new PendingActionRowDto(
            KindCode, id.ToString(CultureInfo.InvariantCulture), name, day, AttendanceFilters.Clock(date.TimeOfDay),
            AttendanceFilters.Join(location, "Ainda sem fotos na galeria"),
            Required: false, Dismissible: true,
            [new PendingActionButtonDto("Adicionar", $"/gallery?upload=1&event={id}&date={day}&title={Uri.EscapeDataString(name)}", true)],
            $"/events/{id}",
            DismissLabel: "Não tenho");
    }
}

/// <summary>
/// Novidades (whoever may write news now: <see cref="NewsAuthorization.CanWrite"/> with the database's roles): one
/// optional prompt per Portugal ISO week. "Criar novidade" opens the /news composer (the member chooses Público or
/// Apenas RTUB there; drafts and publishing keep the module's rules). A post the member saved this week (draft
/// included) resolves it; "Agora não" dismisses it for the week. Nothing is ever published automatically.
/// </summary>
public sealed class NewsPendingActionSource : IPendingActionSource
{
    public const string KindCode = "news.week";

    public string Panel => "news";
    public string Title => "Novidades";
    public string Category => "participation";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => NewsAuthorization.CanWrite(member.ToPrincipal());

    public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) =>
        await PendingAsync(db, member) ? 1 : 0;

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take) =>
        skip == 0 && await PendingAsync(db, member) ? [Row()] : [];

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == KindCode && sourceKey == "" && await PendingAsync(db, member) ? (Row(), Week(member.Today)) : null;

    /// <summary>The ISO week of a Portugal date, e.g. "2026-W45" (the dismissal period).</summary>
    internal static string Week(DateTime today) =>
        $"{ISOWeek.GetYear(today).ToString(CultureInfo.InvariantCulture)}-W{ISOWeek.GetWeekOfYear(today).ToString("00", CultureInfo.InvariantCulture)}";

    private static async Task<bool> PendingAsync(ApplicationDbContext db, PendingActionMember member)
    {
        var week = Week(member.Today);
        if (await db.PendingActionDismissals.AnyAsync(d =>
                d.UserId == member.UserId && d.ActionKind == KindCode && d.SourceKey == "" && d.Period == week))
        {
            return false;
        }

        // Monday 00:00 to the next Monday, Portugal time, as UTC (CreatedAt is stored in UTC).
        var monday = member.Today.AddDays(-(((int)member.Today.DayOfWeek + 6) % 7));
        var (from, to) = (PortugalTime.ToUtc(monday), PortugalTime.ToUtc(monday.AddDays(7)));
        return !await db.NewsPosts.AnyAsync(p => p.AuthorId == member.UserId && p.CreatedAt >= from && p.CreatedAt < to);
    }

    private static PendingActionRowDto Row() => new(
        KindCode, "", "Há novidades para partilhar?", null, null,
        "Escreve uma novidade desta semana. Escolhes Público ou Apenas RTUB; um rascunho só é publicado quando o publicares.",
        Required: false, Dismissible: true,
        [new PendingActionButtonDto("Criar novidade", "/news?new=1", true)],
        "/news");
}
