using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends push notification reminders for unanswered questions.
/// Runs at configured times (default: 9:00, 14:00, 20:00) to check for pending questions and notify assigned members.
/// </summary>
public class QuestionNotificationBackgroundService : BackgroundService
{
    private readonly ILogger<QuestionNotificationBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly QuestionNotificationOptions _options;

    private const int StartupDelaySeconds = 20;
    private const string DefaultBaseUrl = "https://rtub.pt";

    public QuestionNotificationBackgroundService(
        ILogger<QuestionNotificationBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<QuestionNotificationOptions> options)
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
            "Question notification service started. Check times: {CheckTimes}. Enabled: {Enabled}",
            string.Join(", ", _options.CheckTimes),
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next question notification check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendNotificationsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in question notification service");
            }
        }
    }

    private async Task CheckAndSendNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var questionRepository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        try
        {
            var unansweredQuestions = await questionRepository.GetUnansweredQuestionsForNotificationAsync();
            var questionsList = unansweredQuestions.ToList();

            if (!questionsList.Any())
            {
                _logger.LogInformation("No unanswered questions found for notification");
                return;
            }

            _logger.LogInformation("Found {Count} unanswered questions requiring notification", questionsList.Count);

            // Group questions by assigned member to send consolidated notifications
            var questionsByMember = questionsList.GroupBy(q => q.AssignedMemberId);

            foreach (var memberGroup in questionsByMember)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var memberId = memberGroup.Key;
                var questionCount = memberGroup.Count();

                var notification = new SendPushNotificationDto
                {
                    Title = questionCount == 1 ? "Pergunta Pendente" : $"{questionCount} Perguntas Pendentes",
                    Body = questionCount == 1
                        ? $"Tem uma pergunta à espera da sua resposta de {memberGroup.First().Author?.Nickname ?? "um membro"}"
                        : $"Tem {questionCount} perguntas à espera da sua resposta",
                    Url = $"{DefaultBaseUrl}/questions",
                    Tag = "question-reminder"
                };

                await pushNotificationService.SendToUserAsync(memberId, notification);

                // Update last notification timestamp for each question
                foreach (var question in memberGroup)
                {
                    question.UpdateLastNotificationSent();
                    await questionRepository.UpdateAsync(question);
                }

                _logger.LogInformation(
                    "Notification sent to member {MemberId} for {Count} pending questions",
                    memberId, questionCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for unanswered questions");
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var checkTimes = _options.CheckTimes
            .Select(t => TimeSpan.TryParse(t, out var ts) ? ts : (TimeSpan?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .OrderBy(t => t)
            .ToList();

        if (!checkTimes.Any())
        {
            _logger.LogWarning("No valid check times configured. Using default 09:00");
            checkTimes.Add(new TimeSpan(9, 0, 0));
        }

        // Find the next check time today or tomorrow
        foreach (var time in checkTimes)
        {
            var nextRun = now.Date.Add(time);
            if (nextRun > now)
            {
                return nextRun;
            }
        }

        // All times have passed today, schedule for tomorrow's first time
        return now.Date.AddDays(1).Add(checkTimes.First());
    }
}
