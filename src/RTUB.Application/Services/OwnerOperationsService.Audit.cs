// Owner tools: audit log filters, search, delete, export, logins.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public partial class OwnerOperationsService
{
    // ---------- /owner/tracing ----------

    public async Task<OwnerAuditFilters> GetAuditFiltersAsync() => new(
        (await _audit.GetUserNamesAsync()).ToList(),
        (await _audit.GetEntityTypesAsync()).ToList(),
        (await _audit.GetActionTypesAsync()).ToList());

    public async Task<OwnerAuditPage> GetAuditLogsAsync(OwnerAuditFilter filter)
    {
        var size = AuditPageSizes.Contains(filter.PageSize) ? filter.PageSize : AuditPageSizes[0];
        var page = Math.Max(1, filter.Page);
        // The search (stored changes or entity name) narrows the other filters, with an exact total.
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
    /// Read time only (never saved; the export and the stored name stay raw): a name for the page's old entries
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
    /// The filtered log as exported (indented JSON of the entries; the search is not applied), except that
    /// redacted properties of old entries are hidden as on the page. The entries are detached copies: the
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
    /// first, from LoginEvents: sign-ins, active days, first and last time, and the sign-in times (newest
    /// first, at most <see cref="LoginTimesMax"/>). A member with no LoginEvents row in the range whose LastLoginDate
    /// falls in it is still listed (<c>FromHistory = false</c>, latest only).
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
}
