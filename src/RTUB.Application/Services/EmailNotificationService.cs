using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Email;

namespace RTUB.Application.Services;

/// <summary>
/// Service for sending email notifications
/// Refactored to follow Single Responsibility Principle
/// </summary>
public partial class EmailNotificationService : IEmailNotificationService
{
    /// <summary>
    /// Public performance requests (Pedidos) always go to the RTUB inbox. Not
    /// EmailSettings:RecipientEmail, which production points at the no-reply sender.
    /// </summary>
    public const string RequestNotificationRecipient = "realtunab@gmail.com";

    private readonly ILogger<EmailNotificationService> _logger;
    private readonly EmailConfigurationProvider _configProvider;
    private readonly SmtpClientFactory _smtpFactory;
    private readonly EmailRateLimiter _rateLimiter;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IEventRepertoireService _eventRepertoireService;

    public EmailNotificationService(
        ILogger<EmailNotificationService> logger,
        EmailConfigurationProvider configProvider,
        SmtpClientFactory smtpFactory,
        EmailRateLimiter rateLimiter,
        IEmailTemplateRenderer templateRenderer,
        IEnrollmentService enrollmentService,
        IEventRepertoireService eventRepertoireService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _smtpFactory = smtpFactory ?? throw new ArgumentNullException(nameof(smtpFactory));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));
        _enrollmentService = enrollmentService ?? throw new ArgumentNullException(nameof(enrollmentService));
        _eventRepertoireService = eventRepertoireService ?? throw new ArgumentNullException(nameof(eventRepertoireService));
    }
}
