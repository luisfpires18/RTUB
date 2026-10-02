using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /events/{id}/enrollments management behind the React event page (React track 012E).
/// Same rules, now server-side: Admin or Owner only; "Adicionar Membro" enrolls a member who has not
/// answered as going, with their primary instrument and no notification (as before); any answer can be
/// removed. New since the old page: expelled members are not offered, a duplicate is refused instead of
/// silently overwriting, and a cancelled event takes no new answers (the answer modal already refused them).
/// Nobody's answer is edited here: each member changes their own in the answer modal, as before.
/// </summary>
public sealed class EventParticipantsAdminService : IEventParticipantsAdminService
{
    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IEnrollmentService _enrollments;
    private readonly IMemberInstrumentService _instruments;

    public EventParticipantsAdminService(IDbContextFactory<ApplicationDbContext> contexts, IEnrollmentService enrollments, IMemberInstrumentService instruments)
    {
        _contexts = contexts;
        _enrollments = enrollments;
        _instruments = instruments;
    }

    public async Task<EventResult<EventEnrollmentListDto>> GetAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<EventEnrollmentListDto>(user) is { } refused)
        {
            return refused;
        }

        var cancelled = await CancelledAsync(id);
        return cancelled is null
            ? EventResult<EventEnrollmentListDto>.Fail(EventResultStatus.NotFound)
            : EventResult<EventEnrollmentListDto>.Ok(await ListAsync(id, !cancelled.Value));
    }

    public async Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchMembersAsync(int id, string? query, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventMemberOptionDto>>(user) is { } refused)
        {
            return refused;
        }

        if (await CancelledAsync(id) is null)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Fail(EventResultStatus.NotFound);
        }

        var q = Fold(query);
        if (q.Length == 0)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(Array.Empty<EventMemberOptionDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var answered = db.Enrollments.Where(e => e.EventId == id).Select(e => e.UserId);
        // ponytail: every member in memory (~100) so names fold accents the Portuguese way; page it if it grows.
        var members = await db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled && !answered.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(members
            .Select(u => (u, full: $"{u.FirstName} {u.LastName}".Trim()))
            .Where(x => Fold(x.u.Nickname).Contains(q) || Fold(x.full).Contains(q))
            .OrderBy(x => string.IsNullOrWhiteSpace(x.u.Nickname) ? x.full : x.u.Nickname, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new EventMemberOptionDto(
                x.u.Id,
                string.IsNullOrWhiteSpace(x.u.Nickname) ? (x.full.Length == 0 ? "Membro" : x.full) : x.u.Nickname!,
                x.full.Length == 0 ? null : x.full,
                EventAgendaService.IsSafeUrl(x.u.ImageUrl) ? x.u.ImageUrl! : EventAgendaService.DefaultAvatar))
            .ToList());
    }

    public async Task<EventResult<EventEnrollmentListDto>> AddAsync(int id, EventEnrollmentAddInput input, ClaimsPrincipal user)
    {
        if (Refusal<EventEnrollmentListDto>(user) is { } refused)
        {
            return refused;
        }

        var cancelled = await CancelledAsync(id);
        if (cancelled is null)
        {
            return EventResult<EventEnrollmentListDto>.Fail(EventResultStatus.NotFound);
        }

        if (cancelled.Value)
        {
            return EventResult<EventEnrollmentListDto>.Fail(EventResultStatus.Closed);
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            var member = await db.Users.AsNoTracking().Where(u => u.Id == input.UserId).Select(u => new { u.IsExpelled }).FirstOrDefaultAsync();
            if (member is null || member.IsExpelled)
            {
                return EventResult<EventEnrollmentListDto>.Invalid("userId", "Escolha um membro da lista.");
            }

            if (await db.Enrollments.AnyAsync(e => e.EventId == id && e.UserId == input.UserId))
            {
                return EventResult<EventEnrollmentListDto>.Invalid("userId", "Este membro já respondeu a esta atuação.");
            }
        }

        // As the old page: going, the primary instrument, no notification (an Admin adding someone by hand).
        var primary = await _instruments.GetPrimaryInstrumentAsync(input.UserId!);
        await _enrollments.CreateEnrollmentAsync(input.UserId!, id, primary?.InstrumentType, null, true, null, skipNotification: true);
        return EventResult<EventEnrollmentListDto>.Ok(await ListAsync(id, canAdd: true));
    }

    public async Task<EventResult<EventEnrollmentListDto>> RemoveAsync(int id, int enrollmentId, ClaimsPrincipal user)
    {
        if (Refusal<EventEnrollmentListDto>(user) is { } refused)
        {
            return refused;
        }

        var cancelled = await CancelledAsync(id);
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (cancelled is null || !await db.Enrollments.AnyAsync(e => e.Id == enrollmentId && e.EventId == id))
            {
                return EventResult<EventEnrollmentListDto>.Fail(EventResultStatus.NotFound);
            }
        }

        await _enrollments.DeleteEnrollmentAsync(enrollmentId);
        return EventResult<EventEnrollmentListDto>.Ok(await ListAsync(id, !cancelled.Value));
    }

    // ---------- helpers ----------

    private async Task<EventEnrollmentListDto> ListAsync(int id, bool canAdd)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var people = await EventAgendaService.PeopleAsync(db, id);
        static IReadOnlyList<EventManagedEnrollmentDto> Of(IEnumerable<EventAgendaService.Person> p) =>
            p.Select(x => new EventManagedEnrollmentDto(x.EnrollmentId, x.Dto)).ToList();
        return new EventEnrollmentListDto(
            canAdd,
            Of(people.Where(p => p.WillAttend && !p.Leitao)),
            Of(people.Where(p => p.WillAttend && p.Leitao)),
            Of(people.Where(p => !p.WillAttend)));
    }

    /// <summary>Null when the event does not exist.</summary>
    private async Task<bool?> CancelledAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.Where(e => e.Id == id).Select(e => (bool?)e.IsCancelled).FirstOrDefaultAsync();
    }

    private static string Fold(string? text) =>
        new string((text ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant();

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;
}
