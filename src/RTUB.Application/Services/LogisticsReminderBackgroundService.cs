using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RTUB.Application.Services;

/// <summary>Task 069: <c>LogisticsReminders</c> settings. Enabled by default; the integration tests turn it off.</summary>
public sealed class LogisticsReminderOptions
{
    public const string SectionName = "LogisticsReminders";

    public bool Enabled { get; set; } = true;

    /// <summary>How often due reminders are looked for (seconds, at least 30).</summary>
    public int IntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Task 069: runs <see cref="LogisticsReminderDelivery"/> every minute (a fresh scope each run). Safe across restarts and
/// across instances: each occurrence is claimed in the database before it is sent (see the delivery class).
/// </summary>
public sealed class LogisticsReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LogisticsReminderBackgroundService> _logger;
    private readonly LogisticsReminderOptions _options;

    public LogisticsReminderBackgroundService(IServiceScopeFactory scopes, ILogger<LogisticsReminderBackgroundService> logger,
        IOptions<LogisticsReminderOptions> options)
    {
        _scopes = scopes;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Logistics reminders are disabled (LogisticsReminders:Enabled = false)");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(30, _options.IntervalSeconds));
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LogisticsReminderDelivery>().RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Logistics reminder run failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
