using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically settles bets at midnight.
/// Checks for past bets that have a winning option set but user bets may not be fully resolved.
/// Ensures all user bets are marked as PERDEU/GANHOU and fidelis are assigned.
/// </summary>
public class BetSettlementBackgroundService : BackgroundService
{
    private readonly ILogger<BetSettlementBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly BetSettlementOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 30;

    public BetSettlementBackgroundService(
        ILogger<BetSettlementBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BetSettlementOptions> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit before starting to allow the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Bet settlement service started. Will check daily at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next bet settlement check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSettleBetsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bet settlement service");
            }
        }
    }

    private async Task CheckAndSettleBetsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Only run once per day
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Bet settlement already checked today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var betService = scope.ServiceProvider.GetRequiredService<IBetService>();

        try
        {
            // Find past bets that have a winning option set but may need settlement
            // We check for bets where:
            // 1. The bet datetime has passed (IsPast)
            // 2. WinningOptionId is set (admin has defined the victor)
            // 3. There are user bets that might not be resolved yet
            var now = DateTime.UtcNow;
            var pastBetsWithWinners = await context.Bets
                .AsNoTracking()
                .Where(b => !b.IsCancelled 
                    && b.DateTime <= now 
                    && b.WinningOptionId.HasValue)
                .ToListAsync(cancellationToken);

            if (!pastBetsWithWinners.Any())
            {
                _logger.LogInformation("No past bets with winning options found that need settlement");
                _lastRunDate = today;
                return;
            }

            _logger.LogInformation(
                "Found {Count} past bets with winning options to check for settlement",
                pastBetsWithWinners.Count);

            int settledCount = 0;
            int skippedCount = 0;

            foreach (var bet in pastBetsWithWinners)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Check if there are any unresolved user bets for this bet
                    var userBets = await context.UserBets
                        .Where(ub => ub.BetId == bet.Id && !ub.IsWon.HasValue)
                        .ToListAsync(cancellationToken);

                    if (!userBets.Any())
                    {
                        // All user bets are already resolved, skip
                        skippedCount++;
                        _logger.LogDebug(
                            "Bet {BetId} ('{Title}') already has all user bets resolved, skipping",
                            bet.Id,
                            bet.Title);
                        continue;
                    }

                    // There are unresolved user bets - call ResolveBetAsync to process them
                    // This will mark user bets as won/lost and assign fidelis
                    _logger.LogInformation(
                        "Settling bet {BetId} ('{Title}') with {UnresolvedCount} unresolved user bets",
                        bet.Id,
                        bet.Title,
                        userBets.Count);

                    if (bet.WinningOptionId.HasValue)
                    {
                        await betService.ResolveBetAsync(bet.Id, bet.WinningOptionId.Value);
                    }
                    settledCount++;

                    _logger.LogInformation(
                        "Successfully settled bet {BetId} ('{Title}')",
                        bet.Id,
                        bet.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to settle bet {BetId} ('{Title}')",
                        bet.Id,
                        bet.Title);
                }
            }

            _logger.LogInformation(
                "Bet settlement completed. Settled: {SettledCount}, Skipped: {SkippedCount}, Total: {TotalCount}",
                settledCount,
                skippedCount,
                pastBetsWithWinners.Count);

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for bet settlement");
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid bet settlement scheduled time format: {ScheduledTime}. Using default 00:00", scheduledTime);
            timeOfDay = new TimeSpan(0, 0, 0); // Default to midnight
        }

        var nextRun = now.Date.Add(timeOfDay);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun;
    }
}
