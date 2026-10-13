using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Schedules the member status reconciliation (<see cref="IMemberStatusService.ReconcileAllMemberStatusesAsync"/>): once on
/// startup, then daily at MemberStatusUpdate:ScheduledTime (local time from the injected <see cref="TimeProvider"/>).
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

        // Run initial update on startup to ensure data is available immediately
        // This is especially important after deployment or migration
        await RunUpdateAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _time.GetLocalNow().DateTime;
                var scheduledTime = TimeSpan.Parse(_options.ScheduledTime);
                var nextRun = now.Date.Add(scheduledTime);

                // If we've already passed today's scheduled time, schedule for tomorrow
                if (now.TimeOfDay > scheduledTime)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;

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
