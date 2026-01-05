using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that periodically updates member status for all active members
/// Runs every hour to keep the status cache fresh
/// </summary>
public class MemberStatusUpdateBackgroundService : BackgroundService
{
    private readonly ILogger<MemberStatusUpdateBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(1);

    public MemberStatusUpdateBackgroundService(
        ILogger<MemberStatusUpdateBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Member Status Update Background Service is starting");

        // Wait a bit before first run to let the app fully initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
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
                    _logger.LogInformation("Next update in {Interval}", _updateInterval);
                    _logger.LogInformation("========================================");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating member statuses");
            }

            // Wait for the next update cycle
            await Task.Delay(_updateInterval, stoppingToken);
        }

        _logger.LogInformation("Member Status Update Background Service is stopping");
    }
}
