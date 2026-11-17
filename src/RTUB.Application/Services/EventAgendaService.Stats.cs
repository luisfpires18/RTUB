// Event agenda: statistics, video plays and participant lists.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class EventAgendaService
{
    public async Task<EventResult<EventStatsDto>> GetStatsAsync(DateOnly? from, DateOnly? to, ClaimsPrincipal user)
    {
        if (!EventsAuthorization.IsMember(user))
        {
            return EventResult<EventStatsDto>.Fail(EventResultStatus.SignInRequired);
        }

        // The old modal's rules (EnrollmentStatisticsButton): by the event's first day, "vou" answers only;
        // past = before today, else still to come; the share is over the range's past, not cancelled events.
        var seasonStart = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var start = (from ?? new DateOnly(seasonStart, 9, 1)).ToDateTime(TimeOnly.MinValue);
        var end = (to ?? new DateOnly(seasonStart + 1, 8, 31)).ToDateTime(TimeOnly.MinValue);
        if (end < start)
        {
            return EventResult<EventStatsDto>.Invalid("to", "A data de fim não pode ser anterior à de início.");
        }

        var endExclusive = end.AddDays(1);
        var today = Today;
        await using var db = await _contexts.CreateDbContextAsync();
        var pastEvents = await db.Events.CountAsync(e => e.Date >= start && e.Date < endExclusive && e.Date < today && !e.IsCancelled);
        var rows = await db.Enrollments.AsNoTracking()
            .Where(x => x.WillAttend && x.User != null && x.Event!.Date >= start && x.Event.Date < endExclusive)
            .Select(x => new
            {
                x.UserId,
                x.Event!.Date,
                x.User!.Nickname,
                x.User.FirstName,
                x.User.LastName,
                x.User.ImageUrl,
                x.User.Categories,
            })
            .ToListAsync();

        var members = rows.GroupBy(r => r.UserId).Select(g =>
            {
                var r = g.First();
                var categories = r.Categories ?? new List<MemberCategory>();
                var fullName = $"{r.FirstName} {r.LastName}".Trim();
                var groups = new List<string>();
                if (categories.Any(c => c is MemberCategory.Tuno or MemberCategory.Veterano or MemberCategory.Tunossauro)) groups.Add("tuno");
                if (categories.Contains(MemberCategory.Caloiro)) groups.Add("caloiro");
                if (categories.Contains(MemberCategory.Leitao)) groups.Add("leitao");
                return new EventMemberStatsDto(
                    string.IsNullOrWhiteSpace(r.Nickname) ? (string.IsNullOrEmpty(fullName) ? "Membro" : fullName) : r.Nickname,
                    string.IsNullOrEmpty(fullName) ? null : fullName,
                    IsSafeUrl(r.ImageUrl) ? r.ImageUrl! : DefaultAvatar,
                    categories.Select(StatusHelper.GetCategoryDisplay).ToList(),
                    groups,
                    g.Count(x => x.Date < today),
                    g.Count(x => x.Date >= today));
            })
            .OrderByDescending(m => m.Went).ThenByDescending(m => m.Going).ThenBy(m => m.Name, StringComparer.CurrentCulture)
            .ToList();

        return EventResult<EventStatsDto>.Ok(new EventStatsDto(DateText(start), DateText(end), pastEvents, members));
    }

    public async Task<bool> RecordVideoPlayAsync(int videoId, ClaimsPrincipal user)
    {
        var isMember = EventsAuthorization.IsMember(user);
        await using var db = await _contexts.CreateDbContextAsync();
        var video = await db.EventVideos.AsNoTracking()
            .Where(v => v.Id == videoId && (v.Event.IsPublic || isMember))
            .Select(v => new { v.Id, v.Title, EventName = v.Event.Name })
            .FirstOrDefaultAsync();
        if (video is null)
        {
            return false;
        }

        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = "EventVideo",
                EntityId = video.Id,
                Action = "Played",
                UserId = EventsAuthorization.UserId(user),
                UserName = EventsAuthorization.IsMember(user) ? user.Identity!.Name : null,
                Timestamp = DateTime.UtcNow,
                EntityDisplayName = $"{video.Title ?? "Video"} ({video.EventName})",
            });
        }
        catch (Exception ex)
        {
            // A play is never refused because its audit row could not be written.
            _logger.LogWarning(ex, "Failed to audit an event video play ({VideoId})", videoId);
        }

        return true;
    }

    private static EventParticipantsDto Participants(List<Person> people) =>
        new(
            people.Where(p => p.WillAttend && !p.Leitao).Select(p => p.Dto).ToList(),
            people.Where(p => p.WillAttend && p.Leitao).Select(p => p.Dto).ToList(),
            people.Where(p => !p.WillAttend).Select(p => p.Dto).ToList());

    /// <summary>One answer as "Quem vai" shows it, with its row id (only the Admin/Owner manager sends that, 012E).</summary>
    internal sealed record Person(int EnrollmentId, bool WillAttend, bool Leitao, EventParticipantDto Dto);

    /// <summary>Every answer of an event, newest first, in the shape "Quem vai" shows.</summary>
    internal static async Task<List<Person>> PeopleAsync(ApplicationDbContext db, int eventId)
    {
        var rows = await db.Enrollments.AsNoTracking()
            .Where(x => x.EventId == eventId && x.User != null)
            .OrderByDescending(x => x.EnrolledAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.WillAttend,
                x.Instrument,
                x.Notes,
                x.CategoryAtEvent,
                x.User!.Nickname,
                x.User.FirstName,
                x.User.LastName,
                x.User.ImageUrl,
                x.User.Categories,
                x.User.Positions,
            })
            .ToListAsync();

        return rows.Select(r =>
        {
            // The category at the event when it was recorded, else the member's current one (as before).
            var category = r.CategoryAtEvent
                ?? new ApplicationUser { Categories = r.Categories ?? new List<MemberCategory>() }.GetPrimaryCategory();
            var fullName = $"{r.FirstName} {r.LastName}".Trim();
            var dto = new EventParticipantDto(
                string.IsNullOrWhiteSpace(r.Nickname) ? (string.IsNullOrEmpty(fullName) ? "Membro" : fullName) : r.Nickname,
                string.IsNullOrEmpty(fullName) ? null : fullName,
                IsSafeUrl(r.ImageUrl) ? r.ImageUrl! : DefaultAvatar,
                r.Positions?.Contains(Position.Magister) == true ? "MAGISTER" : category is { } c ? StatusHelper.GetCategoryDisplay(c) : null,
                r.WillAttend && r.Instrument is { } i ? StatusHelper.GetInstrumentDisplay(i) : null,
                string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes);
            return new Person(r.Id, r.WillAttend, category == MemberCategory.Leitao, dto);
        }).ToList();
    }

    internal const string DefaultAvatar = "/images/default-avatar.webp";
}
