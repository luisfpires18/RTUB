using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The Owner's operational tools (task 035, docs/react-owner-operations.md), over the services the Blazor pages used:
/// announcement emails, custom push, accounts and roles, the audit log and the database viewer. The caller is already
/// known to be the Owner: <c>OwnerEndpoints</c> refuses everyone else (401 / 403) before reaching this service.
/// </summary>
public interface IOwnerOperationsService
{
    // ---------- /emails ----------
    Task<OwnerEmailRecipients> GetEmailRecipientsAsync();
    Task<EventResult<OwnerSubscriptionResult>> SetSubscriptionAsync(OwnerSubscriptionInput input);
    Task<EventResult<OwnerEmailResult>> SendEmailAsync(OwnerEmailInput input);

    // ---------- /notifications ----------
    Task<OwnerPushRecipients> GetPushRecipientsAsync();
    Task<EventResult<OwnerPushResult>> SendPushAsync(OwnerPushInput input, ClaimsPrincipal actor, string baseUrl);

    // ---------- /users ----------
    /// <summary><paramref name="staff"/>: only accounts whose role is Owner, Admin or Mod.</summary>
    Task<OwnerUserPage> GetUsersAsync(string? q, int? page, int? pageSize, bool staff = false);
    Task<EventResult<OwnerUserDetail>> GetUserAsync(string id);
    Task<EventResult<OwnerUserDetail>> ChangeRoleAsync(string id, OwnerRoleInput input);
    Task<EventResult<OwnerUserDetail>> UnlockAsync(string id);
    Task<OwnerSyncResult> SyncMemberStatusesAsync();

    // ---------- /owner/tracing ----------
    Task<OwnerAuditFilters> GetAuditFiltersAsync();
    Task<OwnerAuditPage> GetAuditLogsAsync(OwnerAuditFilter filter);
    Task<EventResult<bool>> DeleteAuditLogAsync(int id);
    Task<EventResult<bool>> TruncateAuditLogsAsync(OwnerTruncateInput input);
    Task<string> ExportAuditLogsAsync(OwnerAuditFilter filter);
    Task<EventResult<OwnerLogins>> GetLoginsAsync(string? from, string? to);

    // ---------- /owner/dashboard ----------
    Task<EventResult<OwnerDashboard>> GetDashboardAsync(string? from, string? to);

    // ---------- /owner/db ----------
    Task<IReadOnlyList<string>> GetTablesAsync();
    Task<EventResult<OwnerDbGrid>> GetTableAsync(string table, int? page, int? pageSize);
    Task<EventResult<OwnerDbGrid>> RunSelectAsync(OwnerDbQueryInput input);
    Task<EventResult<OwnerDbModifyResult>> RunModifyAsync(OwnerDbModifyInput input);
}
