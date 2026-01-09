using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that updates member status for all active members once per day at a scheduled time
/// Configured via appsettings.json MemberStatusUpdate section
/// </summary>
public class MemberStatusUpdateBackgroundService : BackgroundService
{
    private readonly ILogger<MemberStatusUpdateBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MemberStatusUpdateOptions _options;

    public MemberStatusUpdateBackgroundService(
        ILogger<MemberStatusUpdateBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<MemberStatusUpdateOptions> options)
    {
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
                var now = DateTime.Now;
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
                await Task.Delay(delay, stoppingToken);

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
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("Member Status Update Background Service is stopping");
    }

    private async Task RunUpdateAsync()
    {
        try
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("Starting member status update cycle at {Time}", DateTime.UtcNow);
            _logger.LogInformation("========================================");

            using (var scope = _serviceProvider.CreateScope())
            {
                var memberStatusService = scope.ServiceProvider
                    .GetRequiredService<IMemberStatusService>();

                var updatedCount = await memberStatusService.UpdateAllMemberStatusesAsync();

                _logger.LogInformation("========================================");
                _logger.LogInformation("Member status update completed. Updated {Count} members", updatedCount);
                _logger.LogInformation("Next update scheduled for tomorrow at {ScheduledTime}", _options.ScheduledTime);
                _logger.LogInformation("========================================");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating member statuses");
        }
    }
}
