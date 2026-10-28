namespace RTUB.Application.DTOs;

// The React Owner tools (task 035, docs/react-owner-operations.md): /emails, /notifications, /users, /owner/tracing and
// /owner/db. Owner only; the endpoint group refuses everyone else before any of these is built.

/// <summary>One account in a recipients list: who, never a password or a role.</summary>
public sealed record OwnerPerson(string Id, string Nickname, string FullName, string? Email, string AvatarUrl);

// ---------- /emails ----------

/// <summary>Accounts with a confirmed address, split by "Notificações por email" (Subscribed).</summary>
public sealed record OwnerEmailRecipients(IReadOnlyList<OwnerPerson> Subscribed, IReadOnlyList<OwnerPerson> NotSubscribed);

/// <summary>Turns "Notificações por email" on or off for these accounts (one or many, as the old page did).</summary>
public sealed record OwnerSubscriptionInput(IReadOnlyList<string>? UserIds, bool Subscribed);

public sealed record OwnerSubscriptionResult(int Changed, IReadOnlyList<string> Errors, OwnerEmailRecipients Recipients);

/// <summary>An announcement: the subject is "[RTUB] {Title}" (≤200), the body ≤5000, as the old form.</summary>
public sealed record OwnerEmailInput(string? Title, string? Content);

/// <summary>What the email sender answered: success, how many went out of how many, and its message on failure.</summary>
public sealed record OwnerEmailResult(bool Success, int Sent, int Total, string? Message);

// ---------- /notifications ----------

/// <summary>Accounts with at least one push subscription, the only ones the page can send to.</summary>
public sealed record OwnerPushRecipients(IReadOnlyList<OwnerPerson> Recipients);

/// <summary>A custom push: title ≤100, body ≤500, to the chosen subscribed accounts.</summary>
public sealed record OwnerPushInput(string? Title, string? Body, IReadOnlyList<string>? UserIds);

public sealed record OwnerPushResult(int Recipients, int Sent, int Failed);

// ---------- /users ----------

public sealed record OwnerUserCard(
    string Id, string UserName, string Nickname, string FullName, string? Email, string AvatarUrl,
    string Role, bool Online, bool Locked, DateTime? LastLoginAt);

public sealed record OwnerUserPage(IReadOnlyList<OwnerUserCard> Items, int Total, int Page, int PageSize);

public sealed record OwnerUserDetail(
    string Id, string UserName, string Nickname, string FullName, string? Email, string AvatarUrl, string Role,
    DateTime? LastLoginAt, bool Subscribed, bool Locked, DateTimeOffset? LockoutEnd, bool RequirePasswordChange);

/// <summary>The one role the account keeps: Member, Mod, Admin or Owner (Owner also gets Admin, as before).</summary>
public sealed record OwnerRoleInput(string? Role);

/// <summary>Members evaluated, of which changed (activated + reformed), and members whose evaluation failed.</summary>
public sealed record OwnerSyncResult(int Updated, int Changed, int Failed);

// ---------- /owner/tracing ----------

public sealed record OwnerAuditFilter(
    string? User, string? ExcludeUser, string? EntityType, string? Action, bool CriticalOnly, string? Q, int Page, int PageSize);

/// <summary>
/// One log entry, as stored. <see cref="CurrentDisplayName"/> (#220) is never stored: for an old entry whose stored name
/// is missing or carries a member id, the name that can be resolved now (current nickname or title); null otherwise.
/// </summary>
public sealed record OwnerAuditEntry(
    int Id, DateTime Timestamp, string? UserName, string EntityType, int? EntityId, string? EntityDisplayName,
    string Action, bool IsCritical, string? Changes, string? CurrentDisplayName = null);

/// <summary>
/// One page of the log. <see cref="Total"/> is exact without a search; with one it is the old page's estimate
/// (one more than this page when the page is full). <see cref="CriticalOnPage"/> counts this page only, as before.
/// </summary>
public sealed record OwnerAuditPage(IReadOnlyList<OwnerAuditEntry> Items, int Total, int Page, int PageSize, int CriticalOnPage);

public sealed record OwnerAuditFilters(IReadOnlyList<string> Users, IReadOnlyList<string> EntityTypes, IReadOnlyList<string> Actions);

/// <summary>Deletes every log, or one user's: <see cref="Confirmation"/> must be "DELETE ALL", as the old dialog asked.</summary>
public sealed record OwnerTruncateInput(string? User, string? Confirmation);

/// <summary>
/// /owner/dashboard (task 050): one value per Portugal day of the range. <see cref="Total"/> is the sum, except for
/// active members, where it is the distinct members of the whole range; <see cref="PreviousTotal"/> is the same figure
/// for the same number of days just before.
/// </summary>
public sealed record OwnerDashboardSeries(string Key, string Label, IReadOnlyList<int> Values, int Total, int PreviousTotal);

public sealed record OwnerDashboardRank(string Label, int Count);

/// <summary>Right now, whatever the range: accounts, signed in within the last hour, members with push on.</summary>
public sealed record OwnerDashboardNow(int Accounts, int Online, int PushMembers);

/// <summary>
/// The whole dashboard for From..To (yyyy-MM-dd, both included). <see cref="Hours"/> is 7 rows (Monday first) of 24
/// Portugal hours: sign-ins plus hours members were seen.
/// </summary>
public sealed record OwnerDashboard(
    string From, string To, IReadOnlyList<string> Days, IReadOnlyList<OwnerDashboardSeries> Series,
    IReadOnlyList<OwnerDashboardRank> TopSongs, IReadOnlyList<OwnerDashboardRank> TopNaipes,
    IReadOnlyList<OwnerDashboardRank> TopMembers, IReadOnlyList<OwnerDashboardRank> Areas,
    IReadOnlyList<OwnerDashboardRank> Actions, IReadOnlyList<IReadOnlyList<int>> Hours, OwnerDashboardNow Now);

/// <summary>
/// One member in the tracing "Logins" dialog (task 050: from LoginEvents). <see cref="LastLoginAt"/> and
/// <see cref="FirstAt"/> are the latest and earliest sign-in or activity in the range; <see cref="ActiveDays"/> counts
/// Portugal days. <see cref="FromHistory"/> is false for a member listed only by LastLoginDate (no LoginEvents row in
/// the range): then the counts are 0 / 1 and <see cref="SignInTimes"/> is empty.
/// </summary>
public sealed record OwnerLoginRecord(
    string UserId, string UserName, string Nickname, string FullName, string AvatarUrl, DateTime LastLoginAt,
    int SignIns, int ActiveDays, DateTime FirstAt, IReadOnlyList<DateTime> SignInTimes, bool FromHistory);

/// <summary>The dialog's list, and when LoginEvents history starts (null: no row yet); days before it are latest-only.</summary>
public sealed record OwnerLogins(IReadOnlyList<OwnerLoginRecord> Records, DateTime? HistorySince);

/// <summary>The system announcement, shown by every open React page that polls /api/announcement (≤500; task 037).</summary>
public sealed record OwnerAnnouncementInput(string? Message);

// ---------- /owner/db ----------

/// <summary>Rows as display strings, in column order: "[NULL]", "[Binary: n bytes]", dates, long text cut at 100.</summary>
public sealed record OwnerDbGrid(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, int Total, int Page, int PageSize);

public sealed record OwnerDbQueryInput(string? Query);

/// <summary>An INSERT / UPDATE / DELETE: only runs with <see cref="Confirmed"/>, which the page sets after its warning.</summary>
public sealed record OwnerDbModifyInput(string? Query, bool Confirmed);

public sealed record OwnerDbModifyResult(int RowsAffected);
