using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// /owner/dashboard (task 050, docs/react-owner-operations.md → Dashboard): counts per Portugal day from what is
/// already stored (audit log, play counts, upload rows, enrollments) plus the LoginEvents history (sign-ins and hours
/// seen, written since 050). Read only. Messaging is never counted.
/// </summary>
public partial class OwnerOperationsService
{
    public const int DashboardMaxDays = 366;
    public const int DashboardDefaultDays = 30;
    private const int DashboardTop = 10;

    public async Task<EventResult<OwnerDashboard>> GetDashboardAsync(string? from, string? to)
    {
        var today = PortugalTime.Today(_time);
        if (!TryDay(to, today, out var last)
            || !TryDay(from, last.AddDays(1 - DashboardDefaultDays), out var first)
            || first > last)
        {
            return EventResult<OwnerDashboard>.Invalid("from", "Escolha datas válidas.");
        }

        var days = (last - first).Days + 1;
        if (days > DashboardMaxDays)
        {
            return EventResult<OwnerDashboard>.Invalid("from", $"Escolha no máximo {DashboardMaxDays} dias.");
        }

        // The range plus the same number of days before it (the "previous period" of every tile), in UTC.
        var previousFirst = first.AddDays(-days);
        var since = PortugalTime.ToUtc(previousFirst);
        var until = PortugalTime.ToUtc(last.AddDays(1));
        var start = PortugalTime.ToUtc(first);

        await using var db = await _contexts.CreateDbContextAsync();

        // ponytail: timestamps are bucketed in memory (one narrow column per source); fine for this tuna's volume,
        // move the grouping into SQL if a year of audit rows ever gets slow.
        var logins = await db.LoginEvents.AsNoTracking()
            .Where(e => e.At >= since && e.At < until)
            .Select(e => new { e.At, e.UserId, e.IsSignIn })
            .ToListAsync();
        // Members' movements only: system actions (status reconciliation, background jobs) are not activity.
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Timestamp >= since && a.Timestamp < until && a.Action != "Played")
            .Where(AuditLogService.IsMemberAction)
            .Select(a => new { a.Timestamp, a.UserName, a.EntityType, a.Action })
            .ToListAsync();
        var songPlays = await db.SongPlayCounts.AsNoTracking()
            .Where(p => p.PlayedAt >= since && p.PlayedAt < until).Select(p => p.PlayedAt).ToListAsync();
        var naipePlays = await db.NaipePlayCounts.AsNoTracking()
            .Where(p => p.PlayedAt >= since && p.PlayedAt < until).Select(p => p.PlayedAt).ToListAsync();
        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(e => e.EnrolledAt >= since && e.EnrolledAt < until).Select(e => e.EnrolledAt).ToListAsync();

        // Uploads: every row that stands for a stored file.
        var uploads = new List<DateTime>();
        uploads.AddRange(await db.GalleryMedia.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.CreatedAt < until).Select(x => x.CreatedAt).ToListAsync());
        uploads.AddRange(await db.NaipeContents.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.CreatedAt < until).Select(x => x.CreatedAt).ToListAsync());
        uploads.AddRange(await db.PostMedia.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.CreatedAt < until).Select(x => x.CreatedAt).ToListAsync());
        uploads.AddRange(await db.MeetingAtaAttachments.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.CreatedAt < until).Select(x => x.CreatedAt).ToListAsync());
        uploads.AddRange(await db.CommentImages.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.CreatedAt < until).Select(x => x.CreatedAt).ToListAsync());

        // Day index of a stored UTC timestamp: 0..days-1 in the range, negative in the previous period.
        int Day(DateTime utc) => (PortugalTime.FromUtc(utc).Date - first).Days;

        OwnerDashboardSeries Count(string key, string label, IEnumerable<DateTime> stamps)
        {
            var values = new int[days];
            var previous = 0;
            foreach (var stamp in stamps)
            {
                var d = Day(stamp);
                if (d >= 0) values[d]++;
                else previous++;
            }

            return new OwnerDashboardSeries(key, label, values, values.Sum(), previous);
        }

        var activeByDay = new HashSet<string>[days];
        for (var i = 0; i < days; i++) activeByDay[i] = [];
        var activeNow = new HashSet<string>();
        var activeBefore = new HashSet<string>();
        foreach (var e in logins)
        {
            var d = Day(e.At);
            if (d >= 0)
            {
                activeByDay[d].Add(e.UserId);
                activeNow.Add(e.UserId);
            }
            else
            {
                activeBefore.Add(e.UserId);
            }
        }

        var series = new List<OwnerDashboardSeries>
        {
            Count("signIns", "Inícios de sessão", logins.Where(e => e.IsSignIn).Select(e => e.At)),
            new("activeMembers", "Membros ativos", activeByDay.Select(s => s.Count).ToList(), activeNow.Count, activeBefore.Count),
            Count("movements", "Movimentos", audit.Select(a => a.Timestamp)),
            Count("uploads", "Uploads", uploads),
            Count("songPlays", "Músicas tocadas", songPlays),
            Count("naipePlays", "Naipes vistos", naipePlays),
            Count("enrollments", "Inscrições em atuações", enrollments),
        };

        var hours = new int[7][];
        for (var i = 0; i < 7; i++) hours[i] = new int[24];
        foreach (var e in logins.Where(e => e.At >= start))
        {
            var local = PortugalTime.FromUtc(e.At);
            hours[((int)local.DayOfWeek + 6) % 7][local.Hour]++;
        }

        var current = audit.Where(a => a.Timestamp >= start).ToList();
        static IReadOnlyList<OwnerDashboardRank> Rank(IEnumerable<string?> keys) => keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => k!)
            .Select(g => new OwnerDashboardRank(g.Key, g.Count()))
            .OrderByDescending(r => r.Count).ThenBy(r => r.Label)
            .Take(DashboardTop)
            .ToList();

        var topSongRows = await db.SongPlayCounts.AsNoTracking()
            .Where(p => p.PlayedAt >= start && p.PlayedAt < until)
            .GroupBy(p => p.Song!.Title)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count).ThenBy(r => r.Label)
            .Take(DashboardTop)
            .ToListAsync();
        var topNaipeRows = await db.NaipePlayCounts.AsNoTracking()
            .Where(p => p.PlayedAt >= start && p.PlayedAt < until)
            .GroupBy(p => p.NaipeContent!.Title)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count).ThenBy(r => r.Label)
            .Take(DashboardTop)
            .ToListAsync();

        var utcNow = _time.GetUtcNow().UtcDateTime;
        var onlineSince = utcNow.AddHours(-1);
        var now = new OwnerDashboardNow(
            await db.Users.CountAsync(),
            await db.Users.CountAsync(u => u.LastLoginDate >= onlineSince),
            await db.PushSubscriptions.Select(p => p.UserId).Distinct().CountAsync());

        return EventResult<OwnerDashboard>.Ok(new OwnerDashboard(
            first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Enumerable.Range(0, days).Select(i => first.AddDays(i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList(),
            series,
            topSongRows.Select(r => new OwnerDashboardRank(r.Label, r.Count)).ToList(),
            topNaipeRows.Select(r => new OwnerDashboardRank(r.Label, r.Count)).ToList(),
            Rank(current.Select(a => a.UserName)),
            Rank(current.Select(a => a.EntityType)),
            Rank(current.Select(a => a.Action)),
            hours, now));
    }
}
