using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// What the Blazor /emails, /notifications, /users, /owner/tracing and /owner/db pages did, behind the React Owner tools
/// (task 035, docs/react-owner-operations.md). The same services do the work (email sender, push sender, audit log,
/// database viewer, member status sync); what was only checked in the browser before (lengths, required fields, the SQL
/// rules, the "DELETE ALL" confirmation) is checked here. Owner only: the endpoints refuse everyone else first.
/// </summary>
public partial class OwnerOperationsService : IOwnerOperationsService
{
    private readonly TimeProvider _time;

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
        ILogger<OwnerOperationsService> logger,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
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
        // #220: the search (stored changes or entity name) narrows the other filters, with an exact total.
        var (paged, total) = await _audit.GetPagedWithCountAsync(
            Blank(filter.User), Blank(filter.ExcludeUser), Blank(filter.EntityType), Blank(filter.Action), null, null,
            filter.CriticalOnly ? true : null, page, size, Blank(filter.Q), filter.HideSystem);
        var logs = paged.ToList();
        var current = await CurrentNamesAsync(logs);

        var items = logs.OrderByDescending(l => l.Timestamp)
            .Select(l => new OwnerAuditEntry(l.Id, DateTime.SpecifyKind(l.Timestamp, DateTimeKind.Utc), l.UserName, l.EntityType, l.EntityId,
                l.EntityDisplayName, l.Action, l.IsCriticalAction, AuditLogAppender.RedactStored(l.EntityType, l.Changes), current.GetValueOrDefault(l.Id)))
            .ToList();
        return new OwnerAuditPage(items, total, page, size, items.Count(l => l.IsCritical));
    }

    private static readonly Regex MemberId =
        new(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);

    /// <summary>The entity types an unnamed old entry can be named from today (simple rows that may still exist).</summary>
    private static readonly HashSet<string> CurrentNameTypes =
        ["NewsPost", "PushSubscription", "Trophy", "FiscalYear", "GalleryMedia", "Transaction", "Discussion", "MeetingAta", "Request"];

    /// <summary>
    /// #220, read time only (never saved; the export and the stored name stay raw): a name for the page's old entries
    /// whose stored name is weak - member ids in it become today's nicknames, and a missing name becomes the entity's
    /// current name when it still exists. One query for the members, then one per entity type present on the page; a
    /// deleted entity stays unnamed.
    /// </summary>
    private async Task<Dictionary<int, string>> CurrentNamesAsync(IReadOnlyList<AuditLog> logs)
    {
        var result = new Dictionary<int, string>();
        var withIds = logs.Where(l => !string.IsNullOrEmpty(l.EntityDisplayName) && MemberId.IsMatch(l.EntityDisplayName)).ToList();
        var missing = logs.Where(l => string.IsNullOrWhiteSpace(l.EntityDisplayName) && l.EntityId.HasValue && CurrentNameTypes.Contains(l.EntityType)).ToList();
        if (withIds.Count == 0 && missing.Count == 0)
        {
            return result;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var userIds = withIds.SelectMany(l => MemberId.Matches(l.EntityDisplayName!).Select(m => m.Value)).Distinct().ToList();
        if (userIds.Count > 0)
        {
            var nicknames = (await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.Nickname, u.UserName }).ToListAsync())
                .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? u.Id : u.Nickname);
            foreach (var log in withIds)
            {
                var resolved = MemberId.Replace(log.EntityDisplayName!, m => nicknames.GetValueOrDefault(m.Value, m.Value));
                if (resolved != log.EntityDisplayName)
                {
                    result[log.Id] = resolved;
                }
            }
        }

        foreach (var group in missing.GroupBy(l => l.EntityType))
        {
            var ids = group.Select(l => l.EntityId!.Value).Distinct().ToList();
            Dictionary<int, string> names = group.Key switch
            {
                "NewsPost" => (await db.NewsPosts.AsNoTracking().Where(n => ids.Contains(n.Id)).Select(n => new { n.Id, n.Title, n.Body }).ToListAsync())
                    .ToDictionary(n => n.Id, n => (string.IsNullOrWhiteSpace(n.Title) ? Cut(n.Body, 60) : n.Title.Trim()) ?? ""),
                "PushSubscription" => (await db.PushSubscriptions.AsNoTracking().Where(s => ids.Contains(s.Id))
                        .Select(s => new { s.Id, s.UserAgent, s.User!.Nickname, s.User.UserName }).ToListAsync())
                    .ToDictionary(s => s.Id, s => Joined(" · ", string.IsNullOrWhiteSpace(s.Nickname) ? s.UserName : s.Nickname,
                        string.IsNullOrWhiteSpace(s.UserAgent) ? null : UserAgentHelper.GetShortUserAgent(s.UserAgent)) ?? ""),
                "Trophy" => (await db.Trophies.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync())
                    .ToDictionary(t => t.Id, t => t.Name),
                "FiscalYear" => (await db.FiscalYears.AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => new { f.Id, f.StartYear, f.EndYear }).ToListAsync())
                    .ToDictionary(f => f.Id, f => $"{f.StartYear}-{f.EndYear}"),
                "GalleryMedia" => (await db.GalleryMedia.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => new { m.Id, m.Title }).ToListAsync())
                    .ToDictionary(m => m.Id, m => m.Title ?? ""),
                "Transaction" => (await db.Transactions.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Description }).ToListAsync())
                    .ToDictionary(t => t.Id, t => t.Description),
                "Discussion" => (await db.Discussions.AsNoTracking().Where(d => ids.Contains(d.Id)).Select(d => new { d.Id, Event = d.Event!.Name }).ToListAsync())
                    .ToDictionary(d => d.Id, d => Joined(" · ", "Discussão", d.Event) ?? ""),
                "MeetingAta" => (await db.MeetingAtas.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.AtaNumber, Meeting = a.Meeting!.Title }).ToListAsync())
                    .ToDictionary(a => a.Id, a => Joined(" · ", string.IsNullOrWhiteSpace(a.AtaNumber) ? "Ata" : $"Ata n.º {a.AtaNumber}", a.Meeting) ?? ""),
                // An outside person's request: the kind of event only, never their name or contacts.
                "Request" => (await db.Requests.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new { r.Id, r.EventType }).ToListAsync())
                    .ToDictionary(r => r.Id, r => Joined(" · ", "Pedido", r.EventType) ?? ""),
                _ => new Dictionary<int, string>(),
            };

            foreach (var log in group)
            {
                if (names.GetValueOrDefault(log.EntityId!.Value) is { Length: > 0 } name)
                {
                    result[log.Id] = name;
                }
            }
        }

        return result;
    }

    private static string? Joined(string separator, params string?[] parts) =>
        parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() is { Count: > 0 } kept ? string.Join(separator, kept) : null;

    private static string? Cut(string? text, int max)
    {
        var flat = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length == 0 ? null : flat.Length > max ? WordCut(flat, max) + "…" : flat;
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

    /// <summary>
    /// The filtered log as the old export wrote it (indented JSON of the entries; the search is not applied), except that
    /// redacted properties of old entries are hidden as on the page (#226 F04). The entries are detached copies: the
    /// stored rows are not touched.
    /// </summary>
    public async Task<string> ExportAuditLogsAsync(OwnerAuditFilter filter)
    {
        var logs = (await _audit.GetAllForExportAsync(
            Blank(filter.User), Blank(filter.ExcludeUser), Blank(filter.EntityType), Blank(filter.Action), null, null,
            filter.CriticalOnly ? true : null, filter.HideSystem)).ToList();
        foreach (var log in logs)
        {
            log.Changes = AuditLogAppender.RedactStored(log.EntityType, log.Changes);
        }

        return JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
    }

    public const int LoginTimesMax = 50;

    /// <summary>
    /// Who signed in or was seen between two Portugal days (yyyy-MM-dd, both included; today when missing), most recent
    /// first, from LoginEvents (task 050): sign-ins, active days, first and last time, and the sign-in times (newest
    /// first, at most <see cref="LoginTimesMax"/>). A member with no LoginEvents row in the range whose LastLoginDate
    /// falls in it is still listed (<c>FromHistory = false</c>, latest only), as before 050.
    /// </summary>
    public async Task<EventResult<OwnerLogins>> GetLoginsAsync(string? from, string? to)
    {
        var today = PortugalTime.Today(_time);
        if (!TryDay(from, today, out var start) || !TryDay(to, today, out var end) || start > end)
        {
            return EventResult<OwnerLogins>.Invalid("from", "Escolha datas válidas.");
        }

        var since = PortugalTime.ToUtc(start);
        var until = PortugalTime.ToUtc(end.AddDays(1));

        await using var db = await _contexts.CreateDbContextAsync();
        var historySince = await db.LoginEvents.MinAsync(e => (DateTime?)e.At);
        var events = await db.LoginEvents.AsNoTracking()
            .Where(e => e.At >= since && e.At < until)
            .Select(e => new { e.UserId, e.At, e.IsSignIn })
            .ToListAsync();
        var byUser = events.GroupBy(e => e.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var latestOnly = await db.Users.AsNoTracking()
            .Where(u => u.LastLoginDate >= since && u.LastLoginDate < until)
            .Select(u => u.Id)
            .ToListAsync();
        var ids = byUser.Keys.Union(latestOnly).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();

        IReadOnlyList<OwnerLoginRecord> records = users
            .Select(u =>
            {
                var nickname = string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "" : u.Nickname;
                var fullName = FullName(u);
                var name = string.IsNullOrWhiteSpace(fullName) ? nickname : fullName;
                if (!byUser.TryGetValue(u.Id, out var rows))
                {
                    var last = Utc(u.LastLoginDate)!.Value;
                    return new OwnerLoginRecord(u.Id, u.UserName ?? "", nickname, name, u.ProfilePictureSrc, last,
                        0, 1, last, [], false);
                }

                var signIns = rows.Where(r => r.IsSignIn).Select(r => Utc(r.At)!.Value).OrderDescending().ToList();
                return new OwnerLoginRecord(u.Id, u.UserName ?? "", nickname, name, u.ProfilePictureSrc,
                    Utc(rows.Max(r => r.At))!.Value, signIns.Count,
                    rows.Select(r => PortugalTime.FromUtc(r.At).Date).Distinct().Count(),
                    Utc(rows.Min(r => r.At))!.Value, signIns.Take(LoginTimesMax).ToList(), true);
            })
            .OrderByDescending(r => r.LastLoginAt)
            .ToList();
        return EventResult<OwnerLogins>.Ok(new OwnerLogins(records, Utc(historySince)));
    }

    private static bool TryDay(string? value, DateTime today, out DateTime day)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            day = today;
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

    /// <summary>The first <paramref name="max"/> characters, back to the last whole word when one ends past half.</summary>
    private static string WordCut(string text, int max)
    {
        var cut = text[..max];
        var space = cut.LastIndexOf(' ');
        return (space > max / 2 ? cut[..space] : cut).TrimEnd(' ', ',', ';', ':', '.');
    }
}
