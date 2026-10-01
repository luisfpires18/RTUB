namespace RTUB.Application.DTOs;

// Contracts of the React Events area (React track 011, docs/react-events.md). Built for the browser
// and the caller: a visitor gets only what the old Blazor /events showed every visitor (name, dates,
// location, type, image, cancelled, trophies, videos). Descriptions, cancellation reasons, counts,
// repertoire and the caller's own enrollment are for signed-in members. No user id, enrollment id,
// note of another member, audit field or EF entity leaves the server. Dates are the stored local
// (Portugal) values as plain "yyyy-MM-dd" / "HH:mm" text, so no time zone can shift them.

/// <summary>
/// The whole agenda: upcoming soonest first, past newest first. <c>Types</c> are the event types to
/// pick from when creating or editing; only for Admin/Owner (null for everyone else).
/// </summary>
public sealed record EventAgendaDto(
    bool IsMember,
    bool CanManage,
    IReadOnlyList<EventSummaryDto> Upcoming,
    IReadOnlyList<EventSummaryDto> Past,
    IReadOnlyList<EventTypeOptionDto>? Types = null);

/// <summary>An event type: <c>Value</c> is the EventType name the API takes, <c>Label</c> what people read.</summary>
public sealed record EventTypeOptionDto(string Value, string Label);

/// <summary>
/// An event as the Admin/Owner edit form loads it. <c>Time</c> is null for a whole-day or multi-day
/// event; <c>EndDate</c> only for a multi-day one. <c>ImageUrl</c> is the current image (https/same-site) or null;
/// the image itself changes through /api/events/{id}/image (012A).
/// </summary>
public sealed record EventEditDto(
    int Id,
    string Name,
    string Date,
    string? Time,
    string? EndDate,
    string Location,
    string Type,
    string Description,
    bool HasImage,
    string? ImageUrl = null);

/// <summary>
/// What the Admin/Owner form sends. Dates are "yyyy-MM-dd", the time "HH:mm". A multi-day event
/// (an <c>EndDate</c> after <c>Date</c>) has no time, as on the old form.
/// </summary>
public sealed record EventInput(
    string? Name,
    string? Date,
    string? Time,
    string? EndDate,
    string? Location,
    string? Type,
    string? Description);

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

/// <summary>
/// One event page. <c>Member</c> is null for visitors. <c>CanManagePrizes</c>: the caller is Admin/Owner
/// and the event is a past festival, where prizes are added (012B).
/// </summary>
public sealed record EventDetailDto(
    bool IsMember,
    bool CanManage,
    EventSummaryDto Event,
    IReadOnlyList<EventVideoDto> Videos,
    EventMemberDetailDto? Member,
    bool CanManagePrizes = false);

/// <summary>A video on the public R2 host, in its stored order.</summary>
public sealed record EventVideoDto(int Id, string Title, string Url, string MimeType);

/// <summary>Member-only detail: why it was cancelled, the repertoire per day and who answered.</summary>
public sealed record EventMemberDetailDto(
    string? CancellationReason,
    int NotGoingCount,
    IReadOnlyList<EventRepertoireDayDto> Repertoire,
    EventParticipantsDto Participants);

/// <summary>
/// "Quem vai", for members only, as the old members' list grouped it: members going, Leitões going,
/// and who is not going; each newest answer first.
/// </summary>
public sealed record EventParticipantsDto(
    IReadOnlyList<EventParticipantDto> Going,
    IReadOnlyList<EventParticipantDto> Leitoes,
    IReadOnlyList<EventParticipantDto> NotGoing);

/// <summary>
/// One answer. <c>Badge</c> is "MAGISTER" or the category label (StatusHelper) at the event; <c>Instrument</c> only for
/// someone going; <c>AvatarUrl</c> is https/same-site or the default avatar. No user id, email or phone.
/// </summary>
public sealed record EventParticipantDto(
    string Name,
    string? FullName,
    string AvatarUrl,
    string? Badge,
    string? Instrument,
    string? Notes);

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
    Forbidden,
    InUse,
}

/// <summary>Outcome of an Events call that a member makes; the endpoint maps it to HTTP.</summary>
public sealed record EventResult<T>(EventResultStatus Status, T? Value = default, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static EventResult<T> Ok(T value) => new(EventResultStatus.Ok, value);
    public static EventResult<T> Fail(EventResultStatus status) => new(status);
    public static EventResult<T> Invalid(string field, string message) =>
        new(EventResultStatus.Invalid, Errors: new Dictionary<string, string[]> { [field] = new[] { message } });
}

// ---------- advanced management (Admin/Owner, React track 012A) ----------

/// <summary>A cropped event image as the browser sends it: WebP (JPEG/PNG where WebP cannot be encoded), at most 5 MB.</summary>
public sealed record EventImageUpload(Stream Content, string FileName, string ContentType, long Length);

/// <summary>Cancelling: the reason is required (≤1000), as on the old page; the email to subscribers is opt-in.</summary>
public sealed record EventCancelInput(string? Reason, bool NotifyByEmail);

/// <summary>
/// Who a notice would reach, as counts only (never names or addresses): email = members with
/// "Notificações por email" on out of every confirmed address; push = members with an active push
/// subscription out of every member, and the same for Leitões and Caloiros only.
/// </summary>
public sealed record EventNoticeAudienceDto(
    int EmailSubscribed,
    int EmailTotal,
    int PushSubscribed,
    int PushTotal,
    int PushLeitoesCaloirosSubscribed,
    int PushLeitoesCaloirosTotal);

/// <summary>
/// One notice, as the old page offered it. <c>Channel</c> "email" (<c>Kind</c> "new" or "reminder") or
/// "push" (a <c>Message</c> ≤500, the event name as title; <c>OnlyLeitoesAndCaloiros</c> narrows the audience).
/// </summary>
public sealed record EventNoticeInput(string? Channel, string? Kind, string? Message, bool OnlyLeitoesAndCaloiros);

/// <summary>What a notice (or the cancellation email) did. <c>Warning</c> when part of it did not go out.</summary>
public sealed record EventNoticeResultDto(int Sent, int Failed, string? Warning);

// ---------- prizes (Admin/Owner, React track 012B) ----------

/// <summary>One prize (a Trophies row) as the management modal edits it: its id and name, nothing else.</summary>
public sealed record EventPrizeDto(int Id, string Name);

/// <summary>A prize as the form sends it: the name, required, at most 200 characters.</summary>
public sealed record EventPrizeInput(string? Name);

// ---------- videos (Admin/Owner, React track 012C) ----------

/// <summary>
/// One video as the management modal edits it: its id and the stored title (null when none was
/// given; the page then shows "Vídeo N"). No URL, size, uploader or storage key.
/// </summary>
public sealed record EventManagedVideoDto(int Id, string? Title);

/// <summary>An uploaded video file as the endpoint reads it (≤100 MB, a video type or extension) and its title.</summary>
public sealed record EventVideoUpload(Stream Content, string FileName, string ContentType, long Length, string? Title);

/// <summary>A rename: blank clears the title, as the old page allowed; at most 200 characters.</summary>
public sealed record EventVideoTitleInput(string? Title);

/// <summary>A new order: every video id of the event, exactly once, first to last.</summary>
public sealed record EventVideoOrderInput(IReadOnlyList<int>? VideoIds);

// ---------- repertoire (Admin/Owner, React track 012D) ----------

/// <summary>
/// The repertoire as the management modal edits it: every day it can hold songs (the event's days,
/// plus any other day that already has some), each with its songs in running order.
/// </summary>
public sealed record EventRepertoireManageDto(IReadOnlyList<EventRepertoireManageDayDto> Days);

/// <summary>One day ("yyyy-MM-dd") and its songs, first to last.</summary>
public sealed record EventRepertoireManageDayDto(string Date, IReadOnlyList<EventRepertoireItemDto> Items);

/// <summary>One repertoire row: its id (for remove / reorder) and the song's title.</summary>
public sealed record EventRepertoireItemDto(int Id, string Title);

/// <summary>A song the caller may add: id, title and its album's title. Only songs Music would show them.</summary>
public sealed record EventRepertoireSongDto(int Id, string Title, string? Album);

/// <summary>Adds a song on one of the event's days ("yyyy-MM-dd").</summary>
public sealed record EventRepertoireAddInput(int SongId, string? Date);

/// <summary>A day's new running order: every repertoire row id of that day, exactly once.</summary>
public sealed record EventRepertoireOrderInput(string? Date, IReadOnlyList<int>? ItemIds);
