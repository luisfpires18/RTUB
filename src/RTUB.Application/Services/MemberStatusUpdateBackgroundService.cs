using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Schedules the member status reconciliation (<see cref="IMemberStatusService.ReconcileAllMemberStatusesAsync"/>): on
/// startup only while the MemberStatus table is empty (task 054), then daily at MemberStatusUpdate:ScheduledTime (local
/// time from the injected <see cref="TimeProvider"/>).
/// Orchestration only - the rules live in <see cref="MemberActivityStatusEvaluator"/>.
/// </summary>
public class MemberStatusUpdateBackgroundService : BackgroundService
{
    private readonly ILogger<MemberStatusUpdateBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MemberStatusUpdateOptions _options;
    private readonly TimeProvider _time;

    public MemberStatusUpdateBackgroundService(
        ILogger<MemberStatusUpdateBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<MemberStatusUpdateOptions> options,
        TimeProvider time)
    {
        _time = time;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Member Status Update Background Service is disabled via configuration");
            return;
        }

        _logger.LogInformation("Member Status Update Background Service is starting. Scheduled time: {ScheduledTime}", _options.ScheduledTime);

        // The first run on startup only while the table is empty (a fresh database, or the first boot after the
        // MemberStatus migration): otherwise yesterday's reconciliation is in place and the daily run is enough. A boot
        // used to pay about two commands per member here, on the cold-start path (task 054).
        if (await MemberStatusesAreEmptyAsync(stoppingToken))
        {
            await RunUpdateAsync();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // The scheduled time is Portugal's clock, summer and winter (#226 F02; was the server's local zone = UTC).
                var scheduledTime = TimeSpan.Parse(_options.ScheduledTime);
                var nextRun = PortugalTime.NextUtc(_time, scheduledTime);
                var delay = nextRun - _time.GetUtcNow().UtcDateTime;

                _logger.LogInformation("Next member status update scheduled for {NextRun} (in {Delay})",
                    nextRun.ToString("yyyy-MM-dd HH:mm:ss"),
                    delay.ToString(@"hh\:mm\:ss"));

                // Wait until the scheduled time
                await Task.Delay(delay, _time, stoppingToken);

                // Run the update
                await RunUpdateAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in member status update scheduling");
                // Wait 1 hour before retrying to avoid rapid failure loops
                await Task.Delay(TimeSpan.FromHours(1), _time, stoppingToken);
            }
        }

        _logger.LogInformation("Member Status Update Background Service is stopping");
    }

    /// <summary>Whether the table has no row yet; a failed check answers true, so a boot never skips the run by mistake.</summary>
    private async Task<bool> MemberStatusesAreEmptyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync(cancellationToken);
            return !await db.MemberStatuses.AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check the MemberStatus table on startup; running the initial update");
            return true;
        }
    }

    /// <summary>One reconciliation run in its own scope; never throws (the scheduler goes on). Internal for the tests.</summary>
    internal async Task<MemberStatusReconciliationResult?> RunUpdateAsync()
    {
        try
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("Starting member status update cycle at {Time}", _time.GetUtcNow());
            _logger.LogInformation("========================================");

            using (var scope = _serviceProvider.CreateScope())
            {
                var memberStatusService = scope.ServiceProvider
                    .GetRequiredService<IMemberStatusService>();

                var run = await memberStatusService.ReconcileAllMemberStatusesAsync();

                _logger.LogInformation("========================================");
                _logger.LogInformation(
                    "Member status update completed: {Evaluated} evaluated, {Activated} activated, {Reformed} reformed, {Unchanged} unchanged, {Excluded} excluded, {Failed} failed",
                    run.Evaluated, run.Activated, run.Reformed, run.Unchanged, run.Excluded, run.Failed);
                if (run.Failed > 0)
                {
                    _logger.LogError("Member status update: {Failed} member(s) failed; see the errors above for their ids", run.Failed);
                }
                _logger.LogInformation("Next update scheduled for tomorrow at {ScheduledTime}", _options.ScheduledTime);
                _logger.LogInformation("========================================");
                return run;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating member statuses");
            return null;
        }
    }
}
