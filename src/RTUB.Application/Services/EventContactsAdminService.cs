using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /events/{id}/contacts behind the React page (React track 013, docs/react-events.md).
/// Same behaviour, rules now server-side: Mod and above record a call (Vai / Não vai / Indeciso, notes ≤500)
/// through <see cref="IEventContactService"/>; an answer creates the member's enrollment (primary instrument,
/// no notification) or flips an existing one, and "repor" clears the call and removes the enrollment, exactly
/// as the old page did. Changed on purpose: reading the list (every member's phone) is Mod and above too
/// (any signed-in member could open the old page), and expelled members are not listed. No schema change.
/// </summary>
public sealed class EventContactsAdminService : IEventContactsAdminService
{
    public const int NotesMax = 500;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IEventContactService _contacts;
    private readonly IEnrollmentService _enrollments;
    private readonly IMemberInstrumentService _instruments;
    private readonly ILogger<EventContactsAdminService> _logger;

    public EventContactsAdminService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IEventContactService contacts,
        IEnrollmentService enrollments,
        IMemberInstrumentService instruments,
        ILogger<EventContactsAdminService> logger)
    {
        _contexts = contexts;
        _contacts = contacts;
        _enrollments = enrollments;
        _instruments = instruments;
        _logger = logger;
    }

    public async Task<EventResult<EventContactsDto>> GetAsync(int eventId, ClaimsPrincipal user) =>
        await RefusalAsync(eventId, user) ?? EventResult<EventContactsDto>.Ok(await LoadAsync(eventId));

    public async Task<EventResult<EventContactsDto>> SaveAsync(int eventId, string userId, EventContactInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        if (!await IsListedAsync(userId))
        {
            return EventResult<EventContactsDto>.Fail(EventResultStatus.NotFound);
        }

        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        if (notes?.Length > NotesMax)
        {
            return EventResult<EventContactsDto>.Invalid("notes", $"As notas não podem exceder {NotesMax} caracteres.");
        }

        await _contacts.MarkAsContactedAsync(eventId, userId, input.WillAttend, notes, EventsAuthorization.UserId(user)!);

        if (input.WillAttend is { } willAttend)
        {
            try
            {
                var existing = await _enrollments.GetEnrollmentByEventAndUserAsync(eventId, userId);
                if (existing is null)
                {
                    var primary = await _instruments.GetPrimaryInstrumentAsync(userId);
                    await _enrollments.CreateEnrollmentAsync(userId, eventId, primary?.InstrumentType, notes, willAttend, skipNotification: true);
                }
                else if (existing.WillAttend != willAttend)
                {
                    var instrument = existing.Instrument ?? (await _instruments.GetPrimaryInstrumentAsync(userId))?.InstrumentType;
                    await _enrollments.UpdateEnrollmentAsync(existing.Id, willAttend, instrument, notes ?? existing.Notes, existing.OtherInstruments);
                }
            }
            catch (Exception ex)
            {
                // As before: the call is recorded even when its enrollment could not be synced.
                _logger.LogWarning(ex, "Failed to sync enrollment for user {UserId} on event {EventId}", userId, eventId);
            }
        }

        return EventResult<EventContactsDto>.Ok(await LoadAsync(eventId));
    }

    public async Task<EventResult<EventContactsDto>> ResetAsync(int eventId, string userId, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        if (await _contacts.GetContactAsync(eventId, userId) is not { IsContacted: true })
        {
            return EventResult<EventContactsDto>.Fail(EventResultStatus.NotFound);
        }

        await _contacts.ResetContactAsync(eventId, userId);
        try
        {
            if (await _enrollments.GetEnrollmentByEventAndUserAsync(eventId, userId) is { } enrollment)
            {
                await _enrollments.DeleteEnrollmentAsync(enrollment.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete enrollment when resetting contact for user {UserId} on event {EventId}", userId, eventId);
        }

        return EventResult<EventContactsDto>.Ok(await LoadAsync(eventId));
    }

    private async Task<EventResult<EventContactsDto>?> RefusalAsync(int eventId, ClaimsPrincipal user)
    {
        if (!EventsAuthorization.IsMember(user))
        {
            return EventResult<EventContactsDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (!EventsAuthorization.CanTrackContacts(user))
        {
            return EventResult<EventContactsDto>.Fail(EventResultStatus.Forbidden);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AnyAsync(e => e.Id == eventId) ? null : EventResult<EventContactsDto>.Fail(EventResultStatus.NotFound);
    }

    private async Task<bool> IsListedAsync(string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AnyAsync(u => u.Id == userId && !u.IsExpelled);
    }

    private async Task<EventContactsDto> LoadAsync(int eventId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var members = await db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled)
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl, u.PhoneNumber })
            .ToListAsync();
        var calls = await db.EventContacts.AsNoTracking()
            .Where(c => c.EventId == eventId && c.IsContacted)
            .ToDictionaryAsync(c => c.UserId);

        var rows = members.Select(m =>
        {
            var who = EventDiscussionBoardService.Author(m.Nickname, m.FirstName, m.LastName, m.ImageUrl, null, null);
            calls.TryGetValue(m.Id, out var call);
            return new EventContactRowDto(m.Id, who.Name, who.FullName, who.AvatarUrl,
                string.IsNullOrWhiteSpace(m.PhoneNumber) ? null : m.PhoneNumber.Trim(),
                call?.WillAttend, call?.Notes,
                call?.ContactedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null);
        }).ToList();

        var byName = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true);
        return new EventContactsDto(
            rows.Where(r => r.ContactedAt is not null).OrderByDescending(r => r.ContactedAt).ToList(),
            rows.Where(r => r.ContactedAt is null).OrderBy(r => r.Name, byName).ToList());
    }
}
