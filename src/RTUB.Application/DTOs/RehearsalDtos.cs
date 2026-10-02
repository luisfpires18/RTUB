namespace RTUB.Application.DTOs;

// Rehearsals (React track 014). Rehearsals use attendance / presença; events use enrollment / inscrição.
// Dates are local "yyyy-MM-dd", times "HH:mm". Outcomes reuse EventResult<T> (a generic result + status).

/// <summary>GET /api/rehearsals: every rehearsal, upcoming soonest first, past newest first.</summary>
public sealed record RehearsalAgendaDto(bool CanManage, IReadOnlyList<RehearsalSummaryDto> Upcoming, IReadOnlyList<RehearsalSummaryDto> Past);

/// <summary>
/// One rehearsal. <c>Past</c>: its day is over (the old page's rule: before today). <c>Approvable</c>: presenças can
/// be approved (past, or today with a start at 21:00 or later). <c>PendingCount</c> only for Admin/Owner.
/// </summary>
public sealed record RehearsalSummaryDto(
    int Id,
    string Date,
    string Start,
    string End,
    string Location,
    string? Theme,
    string? Description,
    bool Cancelled,
    string? CancellationReason,
    bool Past,
    bool Approvable,
    string Season,
    int GoingCount,
    int PendingCount,
    RehearsalMineDto? Mine);

/// <summary>The caller's own presença: "pending" (vou, not approved yet), "approved" or "notGoing".</summary>
public sealed record RehearsalMineDto(string Status, string? Instrument, string? Notes);

/// <summary>GET /api/rehearsals/{id}: the rehearsal, its notes, who goes and the instrument counts.</summary>
public sealed record RehearsalDetailDto(
    RehearsalSummaryDto Rehearsal,
    string? Notes,
    bool CanManage,
    RehearsalAttendanceListDto Attendance,
    IReadOnlyList<RehearsalInstrumentCountDto> Instruments,
    IReadOnlyList<RehearsalInstrumentCountDto> OtherInstruments);

/// <summary>Presenças grouped as the old modal did: members going, Leitões going, then not going.</summary>
public sealed record RehearsalAttendanceListDto(
    IReadOnlyList<RehearsalAttendeeDto> Going,
    IReadOnlyList<RehearsalAttendeeDto> Leitoes,
    IReadOnlyList<RehearsalAttendeeDto> NotGoing);

/// <summary>One presença: its row id, who (no user id, email or phone), its status and what they play.</summary>
public sealed record RehearsalAttendeeDto(
    int Id,
    EventAuthorDto Member,
    string Status,
    string? Instrument,
    string? OtherInstruments,
    string? Notes,
    bool Mine,
    bool CanApprove,
    bool CanRemove);

public sealed record RehearsalInstrumentCountDto(string Instrument, int Count);

/// <summary>The caller's presença form: the rehearsal, its state ("open", "past", "cancelled") and the choices.</summary>
public sealed record RehearsalAttendanceDto(
    RehearsalSummaryDto Rehearsal,
    string State,
    string? Status,
    string? Instrument,
    string? Notes,
    bool IsLeitao,
    IReadOnlyList<EventInstrumentOptionDto> Instruments,
    string? DefaultInstrument);

/// <summary>A member's presença: going or not, an InstrumentType name or null (not playing), a note ≤500.</summary>
public sealed record RehearsalAttendanceInput(bool WillAttend, string? Instrument, string? Notes);

/// <summary>Admin/Owner create (one date) or edit (date ignored): location required ≤200, theme ≤500, description and notes ≤1000.</summary>
public sealed record RehearsalInput(string? Date, string? Location, string? Theme, string? Description, string? Notes);

/// <summary>Admin/Owner: one rehearsal on every Tuesday and Thursday from..to (≤90 days, not before today).</summary>
public sealed record RehearsalRangeInput(string? From, string? To, string? Location, string? Theme, string? Description);

public sealed record RehearsalRangeResultDto(int Created, int Skipped);

public sealed record RehearsalCancelInput(string? Reason);

/// <summary>A push to subscribed members (optionally Leitões and Caloiros only); message ≤500.</summary>
public sealed record RehearsalNoticeInput(string? Message, bool OnlyLeitoesAndCaloiros);

public sealed record RehearsalNoticeAudienceDto(int Subscribed, int Total, int LeitoesCaloirosSubscribed, int LeitoesCaloirosTotal);

public sealed record RehearsalNoticeResultDto(int Sent, int Failed);

/// <summary>Admin/Owner: add a member's presença, approved, with their primary instrument.</summary>
public sealed record RehearsalAttendeeInput(string? UserId);

/// <summary>
/// GET /api/rehearsals/stats (members): per member, approved and pending presenças of the range's past, not
/// cancelled rehearsals; <c>PastRehearsals</c> is the share's base.
/// </summary>
public sealed record RehearsalStatsDto(string From, string To, int PastRehearsals, IReadOnlyList<RehearsalMemberStatsDto> Members);

public sealed record RehearsalMemberStatsDto(
    string Name,
    string? FullName,
    string AvatarUrl,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Groups,
    int Approved,
    int Pending);
