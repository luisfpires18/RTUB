using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Page views for /owner/dashboard (task 089). Every React page asks <c>GET /api/account/me</c> once per load, so the
/// controller hands this counter the request's Referer; the path is normalised (ids → <c>{id}</c>, no query, no hash,
/// lower-case, ≤ 128 chars) and counted in memory per Portugal day. Nothing is written per request:
/// <see cref="PageViewFlushService"/> upserts the buffer once a minute and on shutdown. Same-origin referers only, so
/// a stray request from elsewhere counts nothing. Signed-in members only, and at most <see cref="MaxKeys"/> distinct
/// keys in the buffer (the rest is "/outras"). No member, address or user agent is kept.
/// </summary>
public sealed class PageViewCounter(TimeProvider? time = null)
{
    /// <summary>Distinct (day, path) keys the buffer holds at most; beyond it a new path counts as <see cref="Other"/>.</summary>
    public const int MaxKeys = 300;
    public const string Other = "/outras";

    private static readonly Regex Id = new(@"/\d+(?=/|$)", RegexOptions.Compiled);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<(DateTime Day, string Path), int> _pending = new();

    /// <summary>The route of <paramref name="referer"/> when it is on <paramref name="host"/>; null otherwise.</summary>
    public static string? Normalise(string? referer, string? host)
    {
        if (string.IsNullOrWhiteSpace(referer) || string.IsNullOrWhiteSpace(host)
            || !Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Authority, host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Id.Replace(uri.AbsolutePath, "/{id}").ToLowerInvariant();
        if (path.Length > 1) path = path.TrimEnd('/');
        return path.Length <= 128 ? path : null;
    }

    /// <summary>
    /// Counts one view. The caller passes only a signed-in member's request (visitors are not counted, so an anonymous
    /// client cannot grow the buffer); a path never seen before once the buffer holds <see cref="MaxKeys"/> keys goes
    /// to <see cref="Other"/>, so made-up paths can neither fill memory nor the table.
    /// </summary>
    public void Record(string? referer, string? host)
    {
        if (Normalise(referer, host) is not { } path)
        {
            return;
        }

        var key = (PortugalTime.Today(_time), path);
        if (!_pending.ContainsKey(key) && _pending.Count >= MaxKeys)
        {
            key = (key.Item1, Other);
        }

        _pending.AddOrUpdate(key, 1, (_, n) => n + 1);
    }

    /// <summary>Takes everything counted so far; the caller stores it (and gives it back on failure).</summary>
    public IReadOnlyList<(DateTime Day, string Path, int Count)> Drain()
    {
        var taken = new List<(DateTime, string, int)>();
        foreach (var key in _pending.Keys.ToList())
        {
            if (_pending.TryRemove(key, out var count) && count > 0)
            {
                taken.Add((key.Day, key.Path, count));
            }
        }

        return taken;
    }

    public void Restore(IEnumerable<(DateTime Day, string Path, int Count)> items)
    {
        foreach (var (day, path, count) in items)
        {
            _pending.AddOrUpdate((day, path), count, (_, n) => n + count);
        }
    }

    /// <summary>Upserts the buffer: one statement per (day, path) touched since the last flush.</summary>
    public async Task<int> FlushAsync(IDbContextFactory<ApplicationDbContext> contexts, CancellationToken cancellationToken = default)
    {
        var items = Drain();
        if (items.Count == 0)
        {
            return 0;
        }

        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            foreach (var (day, path, count) in items)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO PageViews (Day, Path, Count) VALUES ({day:yyyy-MM-dd HH:mm:ss}, {path}, {count}) ON CONFLICT(Day, Path) DO UPDATE SET Count = Count + excluded.Count",
                    cancellationToken);
            }

            return items.Count;
        }
        catch
        {
            Restore(items);
            throw;
        }
    }
}

/// <summary>Flushes <see cref="PageViewCounter"/> every minute and once more on shutdown; a failed flush keeps the counts for the next one.</summary>
public sealed class PageViewFlushService(PageViewCounter counter, IServiceScopeFactory scopes, ILogger<PageViewFlushService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushOnceAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down: write what is left, briefly.
        }

        await FlushOnceAsync(CancellationToken.None);
    }

    private async Task FlushOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await counter.FlushAsync(scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Page views could not be stored; kept for the next flush");
        }
    }
}
