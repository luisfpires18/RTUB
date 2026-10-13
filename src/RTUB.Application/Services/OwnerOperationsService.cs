using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// What the Blazor /emails, /notifications, /users, /owner/tracing and /owner/db pages did, behind the React Owner tools
/// (task 035, docs/react-owner-operations.md). The same services do the work (email sender, push sender, audit log,
/// database viewer, member status sync); what was only checked in the browser before (lengths, required fields, the SQL
/// rules, the "DELETE ALL" confirmation) is checked here. Owner only: the endpoints refuse everyone else first.
/// </summary>
public class OwnerOperationsService : IOwnerOperationsService
{
    public const int EmailTitleMax = 200;
    public const int EmailContentMax = 5000;
    public const int PushTitleMax = 100;
    public const int PushBodyMax = 500;
    public const string TruncateConfirmation = "DELETE ALL";

    /// <summary>The roles the old dialog offered, highest first.</summary>
    public static readonly string[] Roles = ["Owner", "Admin", "Mod", "Member"];

    private static readonly int[] UserPageSizes = [20, 40, 60, 80, 100];
    private static readonly int[] AuditPageSizes = [20, 40, 60, 80, 100];
    private static readonly int[] TablePageSizes = [10, 25, 50, 100];

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailNotificationService _email;
    private readonly IPushNotificationService _push;
    private readonly IAuditLogService _audit;
    private readonly IMemberStatusService _memberStatus;
    private readonly IUserProfileService _profiles;
    private readonly IDatabaseViewerService _database;
    private readonly ISqlValidationService _sql;
    private readonly ILogger<OwnerOperationsService> _logger;

    public OwnerOperationsService(
        IDbContextFactory<ApplicationDbContext> contexts,
        UserManager<ApplicationUser> users,
        IEmailNotificationService email,
        IPushNotificationService push,
        IAuditLogService audit,
        IMemberStatusService memberStatus,
        IUserProfileService profiles,
        IDatabaseViewerService database,
        ISqlValidationService sql,
        ILogger<OwnerOperationsService> logger)
    {
        _contexts = contexts;
        _users = users;
        _email = email;
        _push = push;
        _audit = audit;
        _memberStatus = memberStatus;
        _profiles = profiles;
        _database = database;
        _sql = sql;
        _logger = logger;
    }

    // ---------- /emails ----------

    public async Task<OwnerEmailRecipients> GetEmailRecipientsAsync()
    {
        var people = await EmailAccountsAsync();
        return new OwnerEmailRecipients(
            people.Where(u => u.Subscribed).Select(Person).ToList(),
            people.Where(u => !u.Subscribed).Select(Person).ToList());
    }

    public async Task<EventResult<OwnerSubscriptionResult>> SetSubscriptionAsync(OwnerSubscriptionInput input)
    {
        var ids = (input.UserIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
        {
            return EventResult<OwnerSubscriptionResult>.Invalid("userIds", "Escolha pelo menos um utilizador.");
        }

        var changed = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            var user = await _users.FindByIdAsync(id);
            if (user is null)
            {
                continue;
            }

            user.Subscribed = input.Subscribed;
            var result = await _users.UpdateAsync(user);
            if (result.Succeeded)
            {
                changed++;
            }
            else
            {
                errors.Add($"{DisplayName(user)}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        return EventResult<OwnerSubscriptionResult>.Ok(new OwnerSubscriptionResult(changed, errors, await GetEmailRecipientsAsync()));
    }

    public async Task<EventResult<OwnerEmailResult>> SendEmailAsync(OwnerEmailInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Require(errors, "title", input.Title, EmailTitleMax, "O título é obrigatório.");
        Require(errors, "content", input.Content, EmailContentMax, "O conteúdo é obrigatório.");
        if (errors.Count > 0)
        {
            return new EventResult<OwnerEmailResult>(EventResultStatus.Invalid, Errors: errors);
        }

        // Who is subscribed now, not when the page was opened; one email per address.
        var recipients = (await EmailAccountsAsync())
            .Where(u => u.Subscribed)
            .DistinctBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipients.Count == 0)
        {
            return EventResult<OwnerEmailResult>.Invalid("recipients", "Não há utilizadores subscritos para enviar o email.");
        }

        var (success, count, message) = await _email.SendAnnouncementEmailAsync(
            input.Title!,
            input.Content!,
            recipients.Select(u => u.Email!).ToList(),
            recipients.ToDictionary(u => u.Email!, u => (u.Nickname ?? u.FirstName ?? "", $"{u.FirstName} {u.LastName}"), StringComparer.OrdinalIgnoreCase));

        return EventResult<OwnerEmailResult>.Ok(new OwnerEmailResult(success, count, recipients.Count, success ? null : message ?? "Erro ao enviar emails."));
    }

    /// <summary>Accounts with a confirmed address, as the old page listed them.</summary>
    private async Task<List<ApplicationUser>> EmailAccountsAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .Where(u => u.EmailConfirmed && u.Email != null && u.Email != "")
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToListAsync();
    }

    // ---------- /notifications ----------

    public async Task<OwnerPushRecipients> GetPushRecipientsAsync() =>
        new((await PushAccountsAsync()).Select(Person).ToList());

    public async Task<EventResult<OwnerPushResult>> SendPushAsync(OwnerPushInput input, ClaimsPrincipal actor, string baseUrl)
    {
        var errors = new Dictionary<string, string[]>();
        Require(errors, "title", input.Title, PushTitleMax, "O título é obrigatório.");
        Require(errors, "body", input.Body, PushBodyMax, "A mensagem é obrigatória.");
        if (errors.Count > 0)
        {
            return new EventResult<OwnerPushResult>(EventResultStatus.Invalid, Errors: errors);
        }

        // Only accounts the page could list (with a push subscription).
        var subscribed = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        var userIds = (input.UserIds ?? []).Where(subscribed.Contains).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return EventResult<OwnerPushResult>.Invalid("userIds", "Selecione pelo menos um utilizador para receber a notificação push.");
        }

        var notification = new SendPushNotificationDto
        {
            Title = input.Title!,
            Body = input.Body!,
            Icon = "/icons/rtub-logo-192.png",
            Url = baseUrl.TrimEnd('/'),
            Tag = $"announcement-{DateTime.UtcNow.Ticks}",
        };
        var (sent, failed) = await _push.SendToSelectedUsersAsync(userIds, notification);

        // Audited as before; a failure here never fails the send.
        try
        {
            var actorId = actor.FindFirstValue(ClaimTypes.NameIdentifier);
            var actorName = actor.Identity?.Name;
            if (!string.IsNullOrEmpty(actorId) && !string.IsNullOrEmpty(actorName))
            {
                await _audit.AddAsync(new AuditLog
                {
                    EntityType = "Notification",
                    EntityId = null,
                    Action = "PushNotificationSent",
                    UserId = actorId,
                    UserName = actorName,
                    Timestamp = DateTime.UtcNow,
                    Changes = JsonSerializer.Serialize(new { RecipientCount = userIds.Count, NotificationBody = notification.Body }),
                    EntityDisplayName = notification.Title,
                    IsCriticalAction = false,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for push notification sent");
        }

        return EventResult<OwnerPushResult>.Ok(new OwnerPushResult(userIds.Count, sent, failed));
    }

    private async Task<List<ApplicationUser>> PushAccountsAsync()
    {
        var ids = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToListAsync();
    }

    // ---------- /users ----------

    public async Task<OwnerUserPage> GetUsersAsync(string? q, int? page, int? pageSize)
    {
        var size = UserPageSizes.Contains(pageSize ?? 0) ? pageSize!.Value : UserPageSizes[0];
        List<ApplicationUser> users;
        Dictionary<string, string> roles;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            users = await db.Users.AsNoTracking().ToListAsync();
            roles = await PrimaryRolesAsync(db);
        }

        var search = new SearchHelper<ApplicationUser> { SearchTerm = q?.Trim() ?? string.Empty };
        var filtered = search.FilterMultiple(users,
            [u => u.Id ?? "", u => u.FirstName ?? "", u => u.LastName ?? "", u => u.Nickname ?? "", u => u.UserName ?? "", u => u.Email ?? ""])
            .OrderByDescending(IsOnline)
            .ThenByDescending(u => u.LastLoginDate ?? DateTime.MinValue)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToList();

        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)size));
        var current = Math.Clamp(page ?? 1, 1, pages);
        var items = filtered.Skip((current - 1) * size).Take(size)
            .Select(u => new OwnerUserCard(u.Id, u.UserName ?? "", u.Nickname ?? "", FullName(u), u.Email, u.ProfilePictureSrc,
                roles.GetValueOrDefault(u.Id, NoRole), IsOnline(u), IsLocked(u), Utc(u.LastLoginDate)))
            .ToList();

        return new OwnerUserPage(items, filtered.Count, current, size);
    }

    public async Task<EventResult<OwnerUserDetail>> GetUserAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        return user is null ? EventResult<OwnerUserDetail>.Fail(EventResultStatus.NotFound) : EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
    }

    public async Task<EventResult<OwnerUserDetail>> ChangeRoleAsync(string id, OwnerRoleInput input)
    {
        var role = Roles.FirstOrDefault(r => string.Equals(r, input.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (role is null)
        {
            return EventResult<OwnerUserDetail>.Invalid("role", "Escolha Member, Mod, Admin ou Owner.");
        }

        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return EventResult<OwnerUserDetail>.Fail(EventResultStatus.NotFound);
        }

        var current = await _users.GetRolesAsync(user);
        if (PrimaryRole(current) == role)
        {
            return EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
        }

        // As before: the account keeps one role; Owner also gets Admin for the admin features.
        if (current.Count > 0)
        {
            var removed = await _users.RemoveFromRolesAsync(user, current);
            if (!removed.Succeeded)
            {
                return EventResult<OwnerUserDetail>.Invalid("role", $"Erro ao remover funções antigas: {Describe(removed)}");
            }
        }

        var added = await _users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            return EventResult<OwnerUserDetail>.Invalid("role", $"Erro ao adicionar nova função: {Describe(added)}");
        }

        if (role == "Owner")
        {
            await _users.AddToRoleAsync(user, "Admin");
        }

        // A new security stamp: the role change applies on the account's next request, not at its next sign-in.
        await _users.UpdateSecurityStampAsync(user);
        return EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
    }

    public async Task<EventResult<OwnerUserDetail>> UnlockAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return EventResult<OwnerUserDetail>.Fail(EventResultStatus.NotFound);
        }

        var unlocked = await _users.SetLockoutEndDateAsync(user, null);
        if (!unlocked.Succeeded)
        {
            return EventResult<OwnerUserDetail>.Invalid("lockout", $"Erro ao desbloquear conta: {Describe(unlocked)}");
        }

        var reset = await _users.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded)
        {
            return EventResult<OwnerUserDetail>.Invalid("lockout", $"Erro ao reiniciar tentativas: {Describe(reset)}");
        }

        return EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
    }

    public async Task<OwnerSyncResult> SyncMemberStatusesAsync()
    {
        var run = await _memberStatus.ReconcileAllMemberStatusesAsync();
        return new OwnerSyncResult(run.Evaluated + run.Excluded, run.Changed, run.Failed);
    }

    private async Task<OwnerUserDetail> DetailAsync(ApplicationUser user) => new(
        user.Id, user.UserName ?? "", user.Nickname ?? "", FullName(user), user.Email, user.ProfilePictureSrc,
        PrimaryRole(await _users.GetRolesAsync(user)), Utc(user.LastLoginDate), user.Subscribed, IsLocked(user),
        IsLocked(user) ? user.LockoutEnd : null, user.RequirePasswordChange);

    private static async Task<Dictionary<string, string>> PrimaryRolesAsync(ApplicationDbContext db)
    {
        var pairs = await (from link in db.UserRoles
                           join role in db.Roles on link.RoleId equals role.Id
                           select new { link.UserId, role.Name }).ToListAsync();
        return pairs.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => PrimaryRole(g.Select(p => p.Name ?? "").ToList()));
    }

    private const string NoRole = "Sem Função";

    private static string PrimaryRole(IList<string> roles) => Roles.FirstOrDefault(roles.Contains) ?? NoRole;

    /// <summary>Signed in within the last hour, as the old card showed it.</summary>
    private static bool IsOnline(ApplicationUser user) =>
        user.LastLoginDate.HasValue && (DateTime.UtcNow - user.LastLoginDate.Value).TotalHours < 1;

    /// <summary>Login dates are stored in UTC; said so, the page shows them in the reader's time.</summary>
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static bool IsLocked(ApplicationUser user) => user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private static string Describe(IdentityResult result) => string.Join(", ", result.Errors.Select(e => e.Description));

    // ---------- /owner/tracing ----------

    public async Task<OwnerAuditFilters> GetAuditFiltersAsync() => new(
        (await _audit.GetUserNamesAsync()).ToList(),
        (await _audit.GetEntityTypesAsync()).ToList(),
        (await _audit.GetActionTypesAsync()).ToList());

    public async Task<OwnerAuditPage> GetAuditLogsAsync(OwnerAuditFilter filter)
    {
        var size = AuditPageSizes.Contains(filter.PageSize) ? filter.PageSize : AuditPageSizes[0];
        var page = Math.Max(1, filter.Page);
        List<AuditLog> logs;
        int total;
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            // As before, a search looks in the changes only and ignores the other filters; its total is an estimate.
            logs = (await _audit.SearchChangesAsync(filter.Q.Trim(), page, size)).ToList();
            total = (page - 1) * size + logs.Count + (logs.Count == size ? 1 : 0);
        }
        else
        {
            var (paged, count) = await _audit.GetPagedWithCountAsync(
                Blank(filter.User), Blank(filter.ExcludeUser), Blank(filter.EntityType), Blank(filter.Action), null, null,
                filter.CriticalOnly ? true : null, page, size);
            logs = paged.ToList();
            total = count;
        }

        var items = logs.OrderByDescending(l => l.Timestamp)
            .Select(l => new OwnerAuditEntry(l.Id, DateTime.SpecifyKind(l.Timestamp, DateTimeKind.Utc), l.UserName, l.EntityType, l.EntityId,
                l.EntityDisplayName, l.Action, l.IsCriticalAction, l.Changes))
            .ToList();
        return new OwnerAuditPage(items, total, page, size, items.Count(l => l.IsCritical));
    }

    public async Task<EventResult<bool>> DeleteAuditLogAsync(int id)
    {
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (!await db.AuditLogs.AnyAsync(a => a.Id == id))
            {
                return EventResult<bool>.Fail(EventResultStatus.NotFound);
            }
        }

        await _audit.DeleteAsync(id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> TruncateAuditLogsAsync(OwnerTruncateInput input)
    {
        if (input.Confirmation != TruncateConfirmation)
        {
            return EventResult<bool>.Invalid("confirmation", $"Escreva {TruncateConfirmation} para confirmar.");
        }

        if (string.IsNullOrWhiteSpace(input.User))
        {
            await _audit.TruncateAsync();
        }
        else
        {
            await _audit.TruncateByUserAsync(input.User.Trim());
        }

        return EventResult<bool>.Ok(true);
    }

    /// <summary>The filtered log as the old export wrote it (indented JSON of the entries; the search is not applied).</summary>
    public async Task<string> ExportAuditLogsAsync(OwnerAuditFilter filter)
    {
        var logs = await _audit.GetAllForExportAsync(
            Blank(filter.User), Blank(filter.ExcludeUser), Blank(filter.EntityType), Blank(filter.Action), null, null,
            filter.CriticalOnly ? true : null);
        return JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Who last signed in between two days (yyyy-MM-dd, both included; today when missing), newest first.</summary>
    public async Task<EventResult<IReadOnlyList<OwnerLoginRecord>>> GetLoginsAsync(string? from, string? to)
    {
        if (!TryDay(from, out var start) || !TryDay(to, out var end))
        {
            return EventResult<IReadOnlyList<OwnerLoginRecord>>.Invalid("from", "Escolha datas válidas.");
        }

        var users = await _profiles.GetUsersWithLoginBetweenAsync(start, end.AddDays(1).AddTicks(-1));
        IReadOnlyList<OwnerLoginRecord> records = users
            .Where(u => u.LastLoginDate.HasValue)
            .Select(u =>
            {
                var nickname = string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "" : u.Nickname;
                var fullName = FullName(u);
                return new OwnerLoginRecord(u.Id, u.UserName ?? "", nickname, string.IsNullOrWhiteSpace(fullName) ? nickname : fullName,
                    u.ProfilePictureSrc, DateTime.SpecifyKind(u.LastLoginDate!.Value, DateTimeKind.Utc));
            })
            .OrderByDescending(r => r.LastLoginAt)
            .ToList();
        return EventResult<IReadOnlyList<OwnerLoginRecord>>.Ok(records);
    }

    private static bool TryDay(string? value, out DateTime day)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            day = DateTime.Today;
            return true;
        }

        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // ---------- /owner/db ----------

    public async Task<IReadOnlyList<string>> GetTablesAsync() => await _database.GetTableNamesAsync();

    public async Task<EventResult<OwnerDbGrid>> GetTableAsync(string table, int? page, int? pageSize)
    {
        var size = TablePageSizes.Contains(pageSize ?? 0) ? pageSize!.Value : 50;
        var current = Math.Max(1, page ?? 1);
        try
        {
            var total = await _database.GetTableRecordCountAsync(table);
            var columns = await _database.GetTableColumnsAsync(table);
            var rows = await _database.GetTableDataAsync(table, current, size);
            return EventResult<OwnerDbGrid>.Ok(Grid(columns, rows, total, current, size));
        }
        catch (ArgumentException)
        {
            return EventResult<OwnerDbGrid>.Fail(EventResultStatus.NotFound);
        }
    }

    public async Task<EventResult<OwnerDbGrid>> RunSelectAsync(OwnerDbQueryInput input)
    {
        var validation = _sql.ValidateQuery(input.Query ?? string.Empty);
        if (!validation.IsValid)
        {
            return EventResult<OwnerDbGrid>.Invalid("query", validation.ErrorMessage);
        }

        try
        {
            var rows = await _database.ExecuteSelectQueryAsync(input.Query!);
            var columns = rows.Count > 0 ? rows[0].Keys.ToList() : [];
            return EventResult<OwnerDbGrid>.Ok(Grid(columns, rows, rows.Count, 1, Math.Max(rows.Count, 1)));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return EventResult<OwnerDbGrid>.Invalid("query", $"Erro ao executar a consulta: {ex.Message}");
        }
    }

    public async Task<EventResult<OwnerDbModifyResult>> RunModifyAsync(OwnerDbModifyInput input)
    {
        var validation = _sql.ValidateModifyQuery(input.Query ?? string.Empty);
        if (!validation.IsValid)
        {
            return EventResult<OwnerDbModifyResult>.Invalid("query", validation.ErrorMessage);
        }

        if (!input.Confirmed)
        {
            return EventResult<OwnerDbModifyResult>.Invalid("confirmed", "Confirme que quer executar esta alteração: não pode ser desfeita.");
        }

        try
        {
            return EventResult<OwnerDbModifyResult>.Ok(new OwnerDbModifyResult(await _database.ExecuteModifyQueryAsync(input.Query!)));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or ArgumentException)
        {
            return EventResult<OwnerDbModifyResult>.Invalid("query", $"Erro ao executar a consulta: {ex.Message}");
        }
    }

    private static OwnerDbGrid Grid(List<string> columns, List<Dictionary<string, object?>> rows, int total, int page, int pageSize) =>
        new(columns, rows.Select(r => (IReadOnlyList<string>)columns.Select(c => Cell(r.GetValueOrDefault(c))).ToList()).ToList(),
            total, page, pageSize);

    /// <summary>How the old grid showed a value.</summary>
    internal static string Cell(object? value) => value switch
    {
        null => "[NULL]",
        byte[] bytes => $"[Binary: {bytes.Length} bytes]",
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) is { } text ? text.Length > 100 ? text[..100] + "..." : text : "",
    };

    // ---------- shared ----------

    private static OwnerPerson Person(ApplicationUser u) =>
        new(u.Id, string.IsNullOrWhiteSpace(u.Nickname) ? FullName(u) : u.Nickname, FullName(u), u.Email, u.ProfilePictureSrc);

    private static string FullName(ApplicationUser u) => $"{u.FirstName} {u.LastName}".Trim();

    private static string DisplayName(ApplicationUser u) => u.Nickname ?? FullName(u);

    private static void Require(Dictionary<string, string[]> errors, string field, string? value, int max, string missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [missing];
        }
        else if (value.Length > max)
        {
            errors[field] = [$"Máximo de {max} caracteres."];
        }
    }
}
