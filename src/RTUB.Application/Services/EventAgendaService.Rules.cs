// Event agenda: visibility rules, summary mapping and enrollment context.
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class EventAgendaService
{
    // ---------- rules ----------

    private static DateTime LastDay(Event e) => (e.EndDate ?? e.Date).Date;

    private DateTime Today => PortugalTime.Today(_time);

    private bool IsPast(Event e) => LastDay(e) < Today;

    /// <summary>
    /// The one visibility rule: a signed-in member sees every event, anyone else only public
    /// ones. Applied in the query, so a members-only event never leaves the database for a visitor.
    /// </summary>
    internal static IQueryable<Event> VisibleTo(IQueryable<Event> events, ClaimsPrincipal user) =>
        EventsAuthorization.IsMember(user) ? events : events.Where(e => e.IsPublic);

    private IEnumerable<Event> Upcoming(IEnumerable<Event> events) =>
        events.Where(e => !IsPast(e)).OrderBy(e => e.Date).ThenBy(e => e.Id);

    /// <summary>September to August.</summary>
    private static string Season(DateTime date)
    {
        var start = date.Month >= 9 ? date.Year : date.Year - 1;
        return $"{start}-{start + 1}";
    }

    // ---------- mapping ----------

    private sealed record Extras(
        bool IsMember,
        ILookup<int, string> Trophies,
        IReadOnlyDictionary<int, int> Videos,
        IReadOnlyDictionary<int, int> Going,
        IReadOnlyDictionary<int, int> Repertoire,
        IReadOnlyDictionary<int, int> Discussion,
        IReadOnlyDictionary<int, bool> Mine);

    private static async Task<Extras> LoadExtrasAsync(ApplicationDbContext db, List<int> ids, string? userId)
    {
        var trophies = (await db.Trophies.AsNoTracking().Where(t => ids.Contains(t.EventId))
                .Select(t => new { t.EventId, t.Name, t.Id }).ToListAsync())
            .OrderBy(t => t.Name, StringComparer.CurrentCulture).ThenBy(t => t.Id)
            .ToLookup(t => t.EventId, t => t.Name);
        var videos = await CountAsync(db.EventVideos.Where(v => ids.Contains(v.EventId)).Select(v => v.EventId));

        if (userId is null)
        {
            var none = new Dictionary<int, int>();
            return new Extras(false, trophies, videos, none, none, none, new Dictionary<int, bool>());
        }

        var going = await CountAsync(db.Enrollments.Where(x => ids.Contains(x.EventId) && x.WillAttend).Select(x => x.EventId));
        var repertoire = await CountAsync(db.EventRepertoires.Where(r => ids.Contains(r.EventId)).Select(r => r.EventId));
        var discussion = await CountAsync(db.Posts.Where(p => !p.IsDeleted)
            .Join(db.Discussions.Where(d => ids.Contains(d.EventId)), p => p.DiscussionId, d => d.Id, (p, d) => d.EventId));
        var mine = await db.Enrollments.AsNoTracking()
            .Where(x => x.UserId == userId && ids.Contains(x.EventId))
            .ToDictionaryAsync(x => x.EventId, x => x.WillAttend);

        return new Extras(true, trophies, videos, going, repertoire, discussion, mine);
    }

    /// <summary>Rows per event, counted by SQL (<c>GROUP BY</c>): only the ids and their counts travel.</summary>
    private static async Task<IReadOnlyDictionary<int, int>> CountAsync(IQueryable<int> eventIds) =>
        await eventIds.GroupBy(id => id).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);

    private EventSummaryDto ToSummary(Event e, Extras extras) => new(
        e.Id,
        e.Name,
        DateText(e.Date),
        e.Date.TimeOfDay == TimeSpan.Zero ? null : e.Date.ToString("HH:mm", CultureInfo.InvariantCulture),
        e.EndDate is { } end && end.Date > e.Date.Date ? DateText(end) : null,
        e.Location,
        e.Type.GetDisplayName(),
        e.IsCancelled,
        IsPast(e),
        Season(e.Date),
        IsSafeUrl(e.ImageUrl) ? e.ImageUrl : null,
        extras.Videos.GetValueOrDefault(e.Id),
        extras.Trophies[e.Id].ToList(),
        extras.IsMember
            ? new EventMemberSummaryDto(
                e.Description,
                extras.Mine.TryGetValue(e.Id, out var going) ? (going ? "going" : "notGoing") : null,
                extras.Going.GetValueOrDefault(e.Id),
                extras.Repertoire.GetValueOrDefault(e.Id),
                extras.Discussion.GetValueOrDefault(e.Id))
            : null,
            !e.IsPublic);

    private static string DateText(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>An https URL or a same-site path; anything else (http:, javascript:, data:, //host) is dropped.</summary>
    internal static bool IsSafeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && ((url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\"))
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));

    // ---------- enrollment ----------

    private sealed record EnrollmentContext(
        EventSummaryDto Event,
        string State,
        Enrollment? Enrollment,
        bool IsLeitao,
        IReadOnlyList<InstrumentType> Registered,
        IReadOnlyList<InstrumentType> Options,
        InstrumentType? Default)
    {
        public bool CanRemove => State == "past" && Enrollment?.WillAttend == true;

        public EventEnrollmentDto ToDto() => new(
            Event,
            State,
            Enrollment is null ? null : Enrollment.WillAttend ? "going" : "notGoing",
            Enrollment?.WillAttend == true ? Enrollment.Instrument?.ToString() : null,
            Enrollment?.Notes,
            IsLeitao,
            Options.Select(i => new EventInstrumentOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i))).ToList(),
            Default?.ToString(),
            CanRemove);
    }

    /// <summary>
    /// The enrolment rules: an upcoming event that is not cancelled is open
    /// (going / not going, instrument, note); a past one only lets someone who went withdraw;
    /// a cancelled one takes no answers. Null when the event does not exist.
    /// </summary>
    private async Task<EnrollmentContext?> LoadEnrollmentAsync(int id, string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
        {
            return null;
        }

        var extras = await LoadExtrasAsync(db, new List<int> { id }, userId);
        var enrollment = await db.Enrollments.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == id && x.UserId == userId);
        var categories = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Categories).FirstOrDefaultAsync();
        var isLeitao = categories?.Contains(MemberCategory.Leitao) == true;
        var registered = await db.MemberInstruments.AsNoTracking()
            .Where(i => i.MemberId == userId)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
            .Select(i => i.InstrumentType)
            .ToListAsync();

        // The form offers the member's own instruments; a Leitão with none could pick any.
        var options = registered.Count > 0 ? registered : isLeitao ? Enum.GetValues<InstrumentType>().ToList() : new List<InstrumentType>();
        var state = e.IsCancelled ? "cancelled" : IsPast(e) ? "past" : "open";

        return new EnrollmentContext(
            ToSummary(e, extras), state, enrollment, isLeitao, registered, options,
            registered.Count > 0 ? registered[0] : null);
    }
}
