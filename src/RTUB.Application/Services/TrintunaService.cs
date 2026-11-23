using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The React /trintuna (docs/react-trintuna.md): gatherings ("Nº Almoço"), their date poll and Vou / Não vou. Rules in
/// <see cref="TrintunaAuthorization"/>; the caller's place on the access list is read once per call.
/// - Anonymous: SignInRequired (401). Not listed and not Owner: NotFound (404) everywhere.
/// - Passados: historical (the seed) or dated before today (Portugal), newest first then by number (descending).
///   Próximos: the rest, dated soonest first, undated after, by number.
/// - The poll is open while the gathering has no date and is not historical. Settling picks one of its own options.
/// </summary>
public sealed partial class TrintunaService : ITrintunaService
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly TimeProvider _time;

    public TrintunaService(IDbContextFactory<ApplicationDbContext> contexts, TimeProvider? time = null)
    {
        _contexts = contexts;
        _time = time ?? TimeProvider.System;
    }

    public async Task<bool> HasAccessAsync(string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.TrintunaAccesses.AnyAsync(a => a.UserId == userId);
    }

    public async Task<EventResult<TrintunaBoardDto>> GetAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<TrintunaBoardDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        var gatherings = await Gatherings(db).ToListAsync();
        var people = await PeopleAsync(db, gatherings);
        var today = PortugalTime.Today(_time);
        var all = gatherings.Select(g => Map(g, people, user, listed, today)).ToList();

        var past = all.Where(g => g.Past)
            .OrderByDescending(g => g.Date ?? string.Empty, StringComparer.Ordinal)
            .ThenByDescending(g => g.Number)
            .ToList();
        var upcoming = all.Where(g => !g.Past)
            .OrderBy(g => g.Date is null)
            .ThenBy(g => g.Date ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(g => g.Number)
            .ToList();
        return EventResult<TrintunaBoardDto>.Ok(new TrintunaBoardDto(TrintunaAuthorization.CanManage(user), upcoming, past));
    }

    /// <summary>SignInRequired for a visitor, NotFound for a member who may not see the page; else the listed flag.</summary>
    private static async Task<(EventResult<T>? Refused, bool Listed)> SeeAsync<T>(ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (!user.IsMember())
        {
            return (EventResult<T>.Fail(EventResultStatus.SignInRequired), false);
        }

        var me = user.UserId();
        var listed = me is not null && await db.TrintunaAccesses.AnyAsync(a => a.UserId == me);
        return TrintunaAuthorization.CanView(user, listed) ? (null, listed) : (EventResult<T>.Fail(EventResultStatus.NotFound), listed);
    }

    private static IQueryable<Gathering> Gatherings(ApplicationDbContext db) => db.Gatherings
        .AsNoTracking()
        .Include(g => g.PollOptions)
        .Include(g => g.PollVotes)
        .Include(g => g.Participants)
        .AsSplitQuery();

    /// <summary>One gathering as the caller sees it, read again after a write.</summary>
    private async Task<GatheringDto> LoadAsync(ApplicationDbContext db, int id, ClaimsPrincipal user, bool listed)
    {
        var gathering = await Gatherings(db).FirstAsync(g => g.Id == id);
        var people = await PeopleAsync(db, new[] { gathering });
        return Map(gathering, people, user, listed, PortugalTime.Today(_time));
    }

    /// <summary>Organizers' display names and photos, by user id.</summary>
    private static async Task<Dictionary<string, GovernanceMemberDto>> PeopleAsync(ApplicationDbContext db, IEnumerable<Gathering> gatherings)
    {
        var ids = gatherings.Select(g => g.OrganizerUserId).OfType<string>().Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, GovernanceMemberDto>();
        }

        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();
        return users.ToDictionary(u => u.Id, u => GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl));
    }

    private static bool IsPast(Gathering g, DateTime today) => g.IsHistorical || (g.Date is { } date && date.Date < today);

    private static bool PollClosed(Gathering g) => g.IsHistorical || g.Date is not null;

    private static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static GatheringDto Map(Gathering g, Dictionary<string, GovernanceMemberDto> people, ClaimsPrincipal user, bool listed, DateTime today)
    {
        var me = user.UserId();
        var owner = TrintunaAuthorization.CanManage(user);
        var closed = PollClosed(g);
        var options = g.PollOptions.OrderBy(o => o.Date).ToList();
        var mine = g.Participants.FirstOrDefault(p => p.UserId == me);
        var organizer = g.OrganizerUserId is { } organizerId && people.TryGetValue(organizerId, out var who)
            ? new TrintunaPersonDto(who.DisplayName, who.AvatarUrl)
            : null;

        var poll = new GatheringPollDto(
            g.Id,
            "Trintuna",
            "Trintuna",
            "Datas possíveis",
            g.CreatedAt,
            null,
            closed,
            false,
            !closed && options.Count > 0 && TrintunaAuthorization.CanAnswer(user, listed),
            false,
            g.PollVotes.FirstOrDefault(v => v.UserId == me)?.GatheringPollOptionId,
            g.PollVotes.Count,
            options.Select(o => new GatheringPollOptionDto(
                o.Id,
                o.Date.ToString("dddd, d 'de' MMMM 'de' yyyy", Pt),
                Day(o.Date),
                g.PollVotes.Count(v => v.GatheringPollOptionId == o.Id))).ToList());

        return new GatheringDto(
            g.Id,
            g.Number,
            $"{g.Number}º Almoço",
            g.Place,
            g.Description,
            g.Date is { } date ? Day(date) : null,
            organizer,
            owner ? g.OrganizerUserId : null,
            TrintunaAuthorization.CanEdit(user, listed, g.OrganizerUserId),
            IsPast(g, today),
            g.Participants.Count(p => p.WillAttend),
            mine is null ? null : mine.WillAttend ? "going" : "notGoing",
            poll);
    }
}
