using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// "As minhas ações pendentes" daily push (task 078, issue #259; docs/react-pending-actions.md → Daily push). Once a
/// day at <see cref="PendingActionsReminderOptions.ScheduledTime"/> (Portugal time), every account that is not expelled
/// is counted with the page's own query (<see cref="IPendingActionsService.CountPendingAsync"/>: every panel, every
/// filter, every page, dismissals excluded); a member with anything pending gets one summary push, a member with
/// nothing gets nothing (no push, no inbox copy).
///
/// Deduplication: before sending, the member's day is claimed as a <see cref="PendingActionDismissal"/> row
/// (<see cref="ClaimKind"/>, period = the Portugal day) under that table's unique index, so restarts, a second instance
/// or a catch-up run never send twice. The claim comes first, so delivery is at most once: a push service failure after
/// the claim is logged and not retried. A count that fails is not "nothing pending": the member is retried
/// (<see cref="Attempts"/> in all) after the others, then logged as failed, with no claim and no push.
/// </summary>
public sealed class PendingActionsReminderService : BackgroundService
{
    /// <summary>The claim's kind. No pending-action source uses it, so it never hides or adds a row of the page.</summary>
    public const string ClaimKind = "reminder.push";

    public const int Attempts = 3;

    private const int StartupDelaySeconds = 15;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PendingActionsReminderService> _logger;
    private readonly PendingActionsReminderOptions _options;
    private readonly TimeProvider _time;

    public PendingActionsReminderService(IServiceScopeFactory scopes, ILogger<PendingActionsReminderService> logger,
        IOptions<PendingActionsReminderOptions> options, TimeProvider? time = null)
    {
        _scopes = scopes;
        _logger = logger;
        _options = options.Value;
        _time = time ?? TimeProvider.System;
    }

    public sealed record RunSummary(int Evaluated, int Sent, int AlreadySent, int Failed);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), _time, stoppingToken);
        var at = ScheduledTime();
        _logger.LogInformation("Pending actions reminder: daily at {Time} Portugal time. Enabled: {Enabled}", at, _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled && DueDay(at) is { } day)
                {
                    var summary = await RunAsync(day, stoppingToken);
                    _logger.LogInformation(
                        "Pending actions reminder {Day}: {Evaluated} evaluated, {Sent} sent, {Already} already sent, {Failed} failed",
                        day.ToString("yyyy-MM-dd"), summary.Evaluated, summary.Sent, summary.AlreadySent, summary.Failed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Pending actions reminder run failed");
            }

            var delay = PortugalTime.NextUtc(_time, at) - _time.GetUtcNow().UtcDateTime;
            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1), _time, stoppingToken);
        }
    }

    /// <summary>Today (Portugal) when its run time has passed less than the catch-up window ago; else null.</summary>
    internal DateTime? DueDay(TimeSpan at)
    {
        var today = PortugalTime.Today(_time);
        var due = PortugalTime.ToUtc(today + at);
        var late = _time.GetUtcNow().UtcDateTime - due;
        return late >= TimeSpan.Zero && late <= TimeSpan.FromHours(Math.Clamp(_options.CatchUpHours, 0, 23)) ? today : null;
    }

    /// <summary>One day's run. Idempotent per member and day (the claim); one member's failure never stops the others.</summary>
    public async Task<RunSummary> RunAsync(DateTime day, CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();
        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var pending = scope.ServiceProvider.GetRequiredService<IPendingActionsService>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var factory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();
        var period = day.ToString("yyyy-MM-dd");

        List<(string Id, string Name)> users;
        HashSet<string> claimed;
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            users = (await db.Users.AsNoTracking().Where(u => !u.IsExpelled).OrderBy(u => u.Id)
                    .Select(u => new { u.Id, u.Nickname, u.UserName }).ToListAsync(cancellationToken))
                .Select(u => (u.Id, u.Nickname ?? u.UserName ?? u.Id)).ToList();
            claimed = (await db.PendingActionDismissals.AsNoTracking()
                .Where(d => d.ActionKind == ClaimKind && d.SourceKey == "" && d.Period == period)
                .Select(d => d.UserId).ToListAsync(cancellationToken)).ToHashSet();
        }

        var (sent, already, failed) = (0, 0, 0);
        var queue = users.Where(u => !claimed.Contains(u.Id)).ToList();
        already += users.Count - queue.Count;
        for (var attempt = 1; attempt <= Attempts && queue.Count > 0; attempt++)
        {
            var retry = new List<(string Id, string Name)>();
            foreach (var user in queue)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count;
                try
                {
                    count = await pending.CountPendingAsync(user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pending actions reminder: counting {User} failed (attempt {Attempt} of {Attempts})", user.Name, attempt, Attempts);
                    retry.Add(user);
                    continue;
                }

                if (count == 0)
                {
                    continue;
                }

                if (!await ClaimAsync(contexts, user.Id, period, cancellationToken))
                {
                    already++;
                    continue;
                }

                try
                {
                    await push.SendToUserAsync(user.Id, factory.CreatePendingActionsReminderNotification());
                    sent++;
                }
                catch (Exception ex)
                {
                    // Claimed already: not retried (at most once), so a flaky push service never sends twice.
                    failed++;
                    _logger.LogError(ex, "Pending actions reminder: sending to {User} failed; not retried today", user.Name);
                }
            }

            queue = retry;
        }

        foreach (var user in queue)
        {
            failed++;
            _logger.LogError("Pending actions reminder: {User} could not be counted after {Attempts} attempts; nothing sent", user.Name, Attempts);
        }

        return new RunSummary(users.Count, sent, already, failed);
    }

    /// <summary>The member's day, or false when another run (restart, second instance) holds it already.</summary>
    private async Task<bool> ClaimAsync(IDbContextFactory<ApplicationDbContext> contexts, string userId, string period, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.PendingActionDismissals.Add(new PendingActionDismissal
        {
            UserId = userId, ActionKind = ClaimKind, SourceKey = "", Period = period, DismissedAt = _time.GetUtcNow().UtcDateTime,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private TimeSpan ScheduledTime()
    {
        if (TimeSpan.TryParse(_options.ScheduledTime, out var at) && at >= TimeSpan.Zero && at < TimeSpan.FromDays(1))
        {
            return at;
        }

        _logger.LogWarning("Invalid PendingActionsReminder:ScheduledTime {Time}; using 18:00", _options.ScheduledTime);
        return new TimeSpan(18, 0, 0);
    }
}
