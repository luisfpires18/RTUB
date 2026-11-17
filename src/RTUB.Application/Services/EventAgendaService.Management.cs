// Event agenda: create, edit and delete events (Admin/Owner).
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class EventAgendaService
{
    // ---------- management (Admin/Owner) ----------

    private static readonly IReadOnlyList<EventTypeOptionDto> TypeOptions = Enum.GetValues<EventType>()
        .Select(t => new EventTypeOptionDto(t.ToString(), t.GetDisplayName()))
        .OrderBy(t => t.Label, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), false))
        .ToList();

    public async Task<EventResult<EventEditDto>> GetEventForEditAsync(int id, ClaimsPrincipal user)
    {
        if (EditRefusal<EventEditDto>(user) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
        {
            return EventResult<EventEditDto>.Fail(EventResultStatus.NotFound);
        }

        var multiDay = e.EndDate is { } end && end.Date > e.Date.Date;
        return EventResult<EventEditDto>.Ok(new EventEditDto(
            e.Id,
            e.Name,
            DateText(e.Date),
            multiDay || e.Date.TimeOfDay == TimeSpan.Zero ? null : e.Date.ToString("HH:mm", CultureInfo.InvariantCulture),
            multiDay ? DateText(e.EndDate!.Value) : null,
            e.Location,
            e.Type.ToString(),
            e.Description,
            !string.IsNullOrEmpty(e.ImageUrl),
            IsSafeUrl(e.ImageUrl) ? e.ImageUrl : null,
            !e.IsPublic));
    }

    public async Task<EventResult<EventSummaryDto>> CreateEventAsync(EventInput input, ClaimsPrincipal user)
    {
        if (EditRefusal<EventSummaryDto>(user) is { } refused)
        {
            return refused;
        }

        if (Errors(input) is { Count: > 0 } errors)
        {
            return new EventResult<EventSummaryDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var valid = Parse(input);
        var created = await _events.CreateEventAsync(valid.Name, valid.Start, valid.Location, valid.Type, valid.Description, valid.End,
            isPublic: input.MembersOnly != true);

        // The old form announced every new event to everyone; a failed push never fails the create.
        // Every push subscription belongs to a signed-in member (/api/push needs one), so a
        // members-only event announced this way reaches no visitor.
        try
        {
            await _push.BroadcastAsync(_pushFactory.CreateEventNotification(created, isReminder: false));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to announce new event {EventId} by push", created.Id);
        }

        return EventResult<EventSummaryDto>.Ok((await SummaryAsync(created.Id, user))!);
    }

    public async Task<EventResult<EventSummaryDto>> UpdateEventAsync(int id, EventInput input, ClaimsPrincipal user)
    {
        if (EditRefusal<EventSummaryDto>(user) is { } refused)
        {
            return refused;
        }

        if (await SummaryAsync(id, user) is null)
        {
            return EventResult<EventSummaryDto>.Fail(EventResultStatus.NotFound);
        }

        if (Errors(input) is { Count: > 0 } errors)
        {
            return new EventResult<EventSummaryDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var valid = Parse(input);
        await _events.UpdateEventAsync(id, valid.Name, valid.Start, valid.Location, valid.Description, valid.Type, valid.End,
            isPublic: input.MembersOnly is { } membersOnly ? !membersOnly : null);
        return EventResult<EventSummaryDto>.Ok((await SummaryAsync(id, user))!);
    }

    public async Task<EventResult<bool>> DeleteEventAsync(int id, ClaimsPrincipal user)
    {
        if (ManageRefusal<bool>(user) is { } refused)
        {
            return refused;
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (!await db.Events.AnyAsync(e => e.Id == id))
            {
                return EventResult<bool>.Fail(EventResultStatus.NotFound);
            }

            // NERBA orders are RESTRICT in the database: the old page crashed here; answer it instead.
            if (await db.NerbaOrders.AnyAsync(o => o.EventId == id))
            {
                return EventResult<bool>.Fail(EventResultStatus.InUse);
            }
        }

        await _events.DeleteEventAsync(id);
        return EventResult<bool>.Ok(true);
    }

    /// <summary>Create and edit: Mod and above. Delete stays <see cref="ManageRefusal{T}"/> (Admin / Owner).</summary>
    private static EventResult<T>? EditRefusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanEdit(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private static EventResult<T>? ManageRefusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private async Task<EventSummaryDto?> SummaryAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e is null ? null : ToSummary(e, await LoadExtrasAsync(db, new List<int> { id }, EventsAuthorization.UserId(user)));
    }

    private sealed record ValidEvent(string Name, DateTime Start, DateTime? End, string Location, EventType Type, string Description);

    /// <summary>
    /// The old form's values: a multi-day event (an end after the start) starts at midnight with no
    /// time; a one-day event takes its time, or none. Only called once <see cref="Errors"/> is empty.
    /// </summary>
    private static ValidEvent Parse(EventInput input)
    {
        var start = ParseDate(input.Date)!.Value;
        var end = ParseDate(input.EndDate) is { } e && e > start ? e : (DateTime?)null;
        if (end is null && ParseTime(input.Time) is { } time)
        {
            start = start.Add(time);
        }

        return new ValidEvent(input.Name!.Trim(), start, end, input.Location!.Trim(), Enum.Parse<EventType>(input.Type!),
            input.Description?.Trim() ?? string.Empty);
    }

    /// <summary>The Event entity's rules (the old form's annotations), as field errors.</summary>
    private static Dictionary<string, string[]> Errors(EventInput input)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string field, string message) => errors[field] = new[] { message };

        if (string.IsNullOrWhiteSpace(input.Name)) Add("name", "Indique o nome da atuação.");
        else if (input.Name.Trim().Length > 200) Add("name", "O nome não pode exceder 200 caracteres.");
        if (string.IsNullOrWhiteSpace(input.Location)) Add("location", "Indique o local.");
        else if (input.Location.Trim().Length > 200) Add("location", "O local não pode exceder 200 caracteres.");
        if ((input.Description?.Trim().Length ?? 0) > 2000) Add("description", "A descrição não pode exceder 2000 caracteres.");
        if (string.IsNullOrEmpty(input.Type) || int.TryParse(input.Type, out _)
            || !Enum.TryParse<EventType>(input.Type, out var type) || !Enum.IsDefined(type))
        {
            Add("type", "Escolha o tipo de atuação.");
        }

        var start = ParseDate(input.Date);
        if (start is null) Add("date", "Indique uma data válida.");
        if (!string.IsNullOrWhiteSpace(input.Time) && ParseTime(input.Time) is null) Add("time", "Indique uma hora válida.");
        if (!string.IsNullOrWhiteSpace(input.EndDate))
        {
            var end = ParseDate(input.EndDate);
            if (end is null) Add("endDate", "Indique uma data de fim válida.");
            else if (start is not null && end < start) Add("endDate", "A data de fim não pode ser anterior à de início.");
        }

        return errors;
    }

    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static TimeSpan? ParseTime(string? text) =>
        TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : null;
}
