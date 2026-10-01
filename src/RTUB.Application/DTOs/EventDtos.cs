namespace RTUB.Application.DTOs;

// Contracts of the React Events area (React track 011, docs/react-events.md). Built for the browser
// and the caller: a visitor gets only what the old Blazor /events showed every visitor (name, dates,
// location, type, image, cancelled, trophies, videos). Descriptions, cancellation reasons, counts,
// repertoire and the caller's own enrollment are for signed-in members. No user id, enrollment id,
// note of another member, audit field or EF entity leaves the server. Dates are the stored local
// (Portugal) values as plain "yyyy-MM-dd" / "HH:mm" text, so no time zone can shift them.

/// <summary>The whole agenda: upcoming soonest first, past newest first.</summary>
public sealed record EventAgendaDto(
    bool IsMember,
    bool CanManage,
    IReadOnlyList<EventSummaryDto> Upcoming,
    IReadOnlyList<EventSummaryDto> Past);

/// <summary>
/// One event as the agenda shows it. <c>Time</c> is null for a whole-day event, <c>EndDate</c> only for
/// a multi-day one. <c>Season</c> is the fiscal year ("2025-2026", September to August).
/// <c>ImageUrl</c> is null unless it is an https or same-site link. <c>Member</c> is null for visitors.
/// </summary>
public sealed record EventSummaryDto(
    int Id,
    string Name,
    string Date,
    string? Time,
    string? EndDate,
    string Location,
    string Type,
    bool Cancelled,
    bool Past,
    string Season,
    string? ImageUrl,
    int VideoCount,
    IReadOnlyList<string> Trophies,
    EventMemberSummaryDto? Member);

/// <summary>Member-only part of a summary. <c>MyStatus</c>: "going", "notGoing" or null.</summary>
public sealed record EventMemberSummaryDto(
    string Description,
    string? MyStatus,
    int GoingCount,
    int RepertoireCount,
    int DiscussionCount);

/// <summary>One event page. <c>Member</c> is null for visitors.</summary>
public sealed record EventDetailDto(
    bool IsMember,
    bool CanManage,
    EventSummaryDto Event,
    IReadOnlyList<EventVideoDto> Videos,
    EventMemberDetailDto? Member);

/// <summary>A video on the public R2 host, in its stored order.</summary>
public sealed record EventVideoDto(int Id, string Title, string Url, string MimeType);

/// <summary>Member-only detail: why it was cancelled, who is not going (a count) and the repertoire per day.</summary>
public sealed record EventMemberDetailDto(
    string? CancellationReason,
    int NotGoingCount,
    IReadOnlyList<EventRepertoireDayDto> Repertoire);

/// <summary>The songs played on one day of the event, in their running order.</summary>
public sealed record EventRepertoireDayDto(string Date, IReadOnlyList<string> Songs);

/// <summary>
/// The caller's own enrollment page. <c>State</c>: "open" (can answer), "past" or "cancelled".
/// <c>Instruments</c> are the choices the caller may play (their own; every instrument for a Leitão
/// with none registered); <c>DefaultInstrument</c> is their primary one.
/// </summary>
public sealed record EventEnrollmentDto(
    EventSummaryDto Event,
    string State,
    string? Status,
    string? Instrument,
    string? Notes,
    bool IsLeitao,
    IReadOnlyList<EventInstrumentOptionDto> Instruments,
    string? DefaultInstrument,
    bool CanRemove);

public sealed record EventInstrumentOptionDto(string Value, string Label);

/// <summary>The answer a member gives. <c>Instrument</c> is an InstrumentType name or null (not playing).</summary>
public sealed record EventEnrollmentInput(bool WillAttend, string? Instrument, string? Notes);

public enum EventResultStatus
{
    Ok,
    NotFound,
    SignInRequired,
    Closed,
    Invalid,
}

/// <summary>Outcome of an Events call that a member makes; the endpoint maps it to HTTP.</summary>
public sealed record EventResult<T>(EventResultStatus Status, T? Value = default, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static EventResult<T> Ok(T value) => new(EventResultStatus.Ok, value);
    public static EventResult<T> Fail(EventResultStatus status) => new(status);
    public static EventResult<T> Invalid(string field, string message) =>
        new(EventResultStatus.Invalid, Errors: new Dictionary<string, string[]> { [field] = new[] { message } });
}
