using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class EmailNotificationServiceTests : IDisposable
{
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly Mock<ILogger<EmailConfigurationProvider>> _mockConfigLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly IMemoryCache _cache;
    private readonly Mock<IEmailTemplateRenderer> _mockTemplateRenderer;
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IEventRepertoireService> _mockEventRepertoireService;
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<EmailNotificationService>>();
        _mockConfigLogger = new Mock<ILogger<EmailConfigurationProvider>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockTemplateRenderer = new Mock<IEmailTemplateRenderer>();
        _mockEnrollmentService = new Mock<IEnrollmentService>();
        _mockEventRepertoireService = new Mock<IEventRepertoireService>();

        // Setup default configuration (SMTP not configured)
        _mockConfiguration.Setup(x => x["EmailSettings:RecipientEmail"]).Returns("jeans@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns((string?)null);

        // Setup template renderer to return dummy HTML
        _mockTemplateRenderer.Setup(x => x.RenderNewRequestNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync("Test email body");
        
        _mockTemplateRenderer.Setup(x => x.RenderWelcomeEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test welcome email");
        
        _mockTemplateRenderer.Setup(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test event email");
        
        _mockTemplateRenderer.Setup(x => x.RenderEventReminderNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<(string, string, string, string?, bool)>>(), It.IsAny<List<(string, DateTime)>>()))
            .ReturnsAsync("Test reminder email");
        
        _mockTemplateRenderer.Setup(x => x.RenderEventCancellationNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test cancellation email");

        // Setup enrollment service to return empty list by default
        _mockEnrollmentService.Setup(x => x.GetEnrollmentsByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<RTUB.Core.Entities.Enrollment>());
        
        // Setup event repertoire service to return empty list by default
        _mockEventRepertoireService.Setup(x => x.GetRepertoireByEventIdAsync(It.IsAny<int>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<RTUB.Core.Entities.EventRepertoire>());

        // Create the refactored dependencies
        var configProvider = new EmailConfigurationProvider(_mockConfiguration.Object, _mockConfigLogger.Object);
        var smtpFactory = new SmtpClientFactory();
        var rateLimiter = new EmailRateLimiter(_cache);

        _service = new EmailNotificationService(
            _mockLogger.Object,
            configProvider,
            smtpFactory,
            rateLimiter,
            _mockTemplateRenderer.Object,
            _mockEnrollmentService.Object,
            _mockEventRepertoireService.Object);
    }

    [Fact]
    public async Task SendRequestStatusChangedAsync_CompletesSuccessfully()
    {
        // Arrange
        var requestId = 1;
        var requestName = "John Doe";
        var requestEmail = "john@test.com";
        var oldStatus = RequestStatus.Pending;
        var newStatus = RequestStatus.Confirmed;

        // Act
        Func<Task> act = async () => await _service.SendRequestStatusChangedAsync(requestId, requestName, requestEmail, oldStatus, newStatus);

        // Assert - Should complete without throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendNewRequestNotificationAsync_SimpleOverload_CompletesSuccessfully()
    {
        // Arrange
        var requestId = 2;
        var requestName = "Jane Smith";
        var requestEmail = "jane@test.com";
        var eventType = "Wedding";

        // Act
        Func<Task> act = async () => await _service.SendNewRequestNotificationAsync(requestId, requestName, requestEmail, eventType);

        // Assert - Should complete without throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendNewRequestNotificationAsync_FullOverload_DoesNotThrow_WhenSmtpNotConfigured()
    {
        // Arrange
        var requestId = 3;
        var requestName = "Test User";
        var requestEmail = "test@test.com";
        var phone = "123456789";
        var eventType = "Birthday";
        var preferredDate = DateTime.Now.AddDays(7);
        var location = "Lisbon";
        var message = "Test message";
        var createdAt = DateTime.Now;

        // Act
        Func<Task> act = async () => await _service.SendNewRequestNotificationAsync(
            requestId, requestName, requestEmail, phone, eventType,
            preferredDate, null, location, message, createdAt);

        // Assert - Should not throw even when SMTP is not configured
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_DoesNotThrow_WhenSmtpNotConfigured()
    {
        // Arrange
        var userName = "newuser";
        var email = "newuser@test.com";
        var firstName = "New";
        var lastName = "New";
        var password = "TempPassword123";

        // Act
        Func<Task> act = async () => await _service.SendWelcomeEmailAsync(userName, email, firstName, lastName, password);

        // Assert - Should not throw even when SMTP is not configured
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(RequestStatus.Pending, RequestStatus.Analysing)]
    [InlineData(RequestStatus.Analysing, RequestStatus.Confirmed)]
    [InlineData(RequestStatus.Confirmed, RequestStatus.Rejected)]
    public async Task SendRequestStatusChangedAsync_HandlesAllStatusTransitions(RequestStatus oldStatus, RequestStatus newStatus)
    {
        // Arrange
        var requestId = 100;
        var requestName = "Status Test User";
        var requestEmail = "status@test.com";

        // Act
        Func<Task> act = async () => await _service.SendRequestStatusChangedAsync(requestId, requestName, requestEmail, oldStatus, newStatus);

        // Assert - Should handle all status transitions without throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendEventNotificationAsync_DoesNotThrow_WhenSmtpNotConfigured()
    {
        // Arrange
        var eventId = 1;
        var eventTitle = "Test Event";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com" };

        // Act
        var result = await _service.SendEventNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails);

        // Assert - Should not throw even when SMTP is not configured
        result.success.Should().BeFalse();
        result.count.Should().Be(0);
        result.errorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendEventNotificationAsync_ReturnsError_WhenNoRecipients()
    {
        // Arrange
        var eventId = 2;
        var eventTitle = "Test Event 2";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string>();

        // Setup SMTP configuration for this test so it doesn't fail on SMTP config check
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Act
        var result = await _service.SendEventNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails);

        // Assert
        result.success.Should().BeFalse();
        result.count.Should().Be(0);
        result.errorMessage.Should().Contain("Nenhum destinatário");
    }

    [Fact]
    public async Task SendEventNotificationAsync_RateLimits_DuplicateRequests()
    {
        // Arrange
        var eventId = 3;
        var eventTitle = "Test Event 3";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string> { "user1@test.com" };

        // Setup SMTP configuration for this test
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Act - First call
        var result1 = await _service.SendEventNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails);

        // Act - Second call immediately after (should be rate limited)
        var result2 = await _service.SendEventNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails);

        // Assert
        result2.success.Should().BeFalse();
        result2.errorMessage.Should().Contain("já enviado recentemente");
    }

    [Fact]
    public async Task SendEventCancellationNotificationAsync_DoesNotThrow_WhenSmtpNotConfigured()
    {
        // Arrange
        var eventId = 4;
        var eventTitle = "Cancelled Event";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var cancellationReason = "Mau tempo previsto";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com" };
        var recipientData = new Dictionary<string, (string nickname, string fullName)>
        {
            { "user1@test.com", ("user1", "User One") },
            { "user2@test.com", ("user2", "User Two") }
        };

        // Act
        var result = await _service.SendEventCancellationNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, cancellationReason, eventLink, recipientEmails, recipientData);

        // Assert - Should not throw even when SMTP is not configured
        result.success.Should().BeFalse();
        result.count.Should().Be(0);
        result.errorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendEventCancellationNotificationAsync_ReturnsError_WhenNoRecipients()
    {
        // Arrange
        var eventId = 5;
        var eventTitle = "Cancelled Event 2";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var cancellationReason = "Cancelado por motivos internos";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string>();
        var recipientData = new Dictionary<string, (string nickname, string fullName)>();

        // Setup SMTP configuration for this test
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Act
        var result = await _service.SendEventCancellationNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, cancellationReason, eventLink, recipientEmails, recipientData);

        // Assert
        result.success.Should().BeFalse();
        result.count.Should().Be(0);
        result.errorMessage.Should().Contain("Nenhum destinatário");
    }

    [Fact]
    public async Task SendEventCancellationNotificationAsync_RateLimits_DuplicateRequests()
    {
        // Arrange
        var eventId = 6;
        var eventTitle = "Cancelled Event 3";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var cancellationReason = "Motivo de teste";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string> { "user1@test.com" };
        var recipientData = new Dictionary<string, (string nickname, string fullName)>
        {
            { "user1@test.com", ("user1", "User One") }
        };

        // Setup SMTP configuration for this test
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Act - First call
        var result1 = await _service.SendEventCancellationNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, cancellationReason, eventLink, recipientEmails, recipientData);

        // Act - Second call immediately after (should be rate limited)
        var result2 = await _service.SendEventCancellationNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, cancellationReason, eventLink, recipientEmails, recipientData);

        // Assert
        result2.success.Should().BeFalse();
        result2.errorMessage.Should().Contain("já enviado recentemente");
    }
    
    [Fact]
    public async Task SendEventReminderNotificationAsync_DoesNotThrow_WhenSmtpNotConfigured()
    {
        // Arrange
        var eventId = 7;
        var eventTitle = "Reminder Event";
        var eventDate = DateTime.Now.AddDays(3);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com" };
        var recipientData = new Dictionary<string, (string nickname, string fullName)>
        {
            { "user1@test.com", ("user1", "User One") },
            { "user2@test.com", ("user2", "User Two") }
        };

        // Setup mock for RenderEventReminderNotificationAsync
        _mockTemplateRenderer.Setup(x => x.RenderEventReminderNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test reminder email");

        // Act
        var result = await _service.SendEventReminderNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails, recipientData);

        // Assert - Should not throw even when SMTP is not configured
        result.success.Should().BeFalse();
        result.count.Should().Be(0);
        result.errorMessage.Should().NotBeNull();
    }
    
    [Fact]
    public async Task SendEventReminderNotificationAsync_ReturnsError_WhenNoRecipients()
    {
        // Arrange
        var eventId = 8;
        var eventTitle = "Reminder Event 2";
        var eventDate = DateTime.Now.AddDays(3);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string>();

        // Setup SMTP configuration for this test so it doesn't fail on SMTP config check
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Setup mock for RenderEventReminderNotificationAsync
        _mockTemplateRenderer.Setup(x => x.RenderEventReminderNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test reminder email");

        // Act
        var result = await _service.SendEventReminderNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails);

        // Assert
        result.success.Should().BeFalse();
        result.count.Should().Be(0);
        result.errorMessage.Should().Contain("Nenhum destinatário");
    }
    
    [Fact]
    public async Task SendEventReminderNotificationAsync_RateLimits_DuplicateRequests()
    {
        // Arrange
        var eventId = 9;
        var eventTitle = "Reminder Event 3";
        var eventDate = DateTime.Now.AddDays(3);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events";
        var recipientEmails = new List<string> { "user1@test.com" };
        var recipientData = new Dictionary<string, (string nickname, string fullName)>
        {
            { "user1@test.com", ("user1", "User One") }
        };

        // Setup SMTP configuration for this test
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Setup mock for RenderEventReminderNotificationAsync
        _mockTemplateRenderer.Setup(x => x.RenderEventReminderNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test reminder email");

        // Act - First call
        var result1 = await _service.SendEventReminderNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails, recipientData);

        // Act - Second call immediately after (should be rate limited)
        var result2 = await _service.SendEventReminderNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, recipientEmails, recipientData);

        // Assert
        result2.success.Should().BeFalse();
        result2.errorMessage.Should().Contain("já enviado recentemente");
    }
    
    [Fact]
    public async Task SendUsernameChangedEmailAsync_CompletesSuccessfully_WhenSmtpNotConfigured()
    {
        // Arrange
        var email = "leitao@test.com";
        var fullName = "João Silva";
        var nickname = "Jeans";
        var oldUsername = "joaosilva";
        var newUsername = "jeans";
        
        // Setup template renderer for username changed email
        _mockTemplateRenderer.Setup(x => x.RenderUsernameChangedEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test username changed email");

        // Act
        Func<Task> act = async () => await _service.SendUsernameChangedEmailAsync(
            email, fullName, nickname, oldUsername, newUsername);

        // Assert - Should not throw even when SMTP is not configured
        await act.Should().NotThrowAsync();
    }
    
    [Fact]
    public async Task SendUsernameChangedEmailAsync_RateLimits_DuplicateRequests()
    {
        // Arrange
        var email = "leitao@test.com";
        var fullName = "João Silva";
        var nickname = "Jeans";
        var oldUsername = "joaosilva";
        var newUsername = "jeans";
        
        // Setup template renderer for username changed email
        _mockTemplateRenderer.Setup(x => x.RenderUsernameChangedEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test username changed email");
        
        // Setup SMTP configuration
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");

        // Act - First call
        await _service.SendUsernameChangedEmailAsync(email, fullName, nickname, oldUsername, newUsername);
        
        // Act - Second call immediately after (should be rate limited - no effect visible in tests but cache is used)
        Func<Task> act = async () => await _service.SendUsernameChangedEmailAsync(
            email, fullName, nickname, oldUsername, newUsername);

        // Assert - Should not throw (rate limiting is silent for void methods)
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendEventNotificationAsync_WithProgressCallback_AcceptsProgressParameter()
    {
        // Arrange
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" };
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email.Split('@')[0]}", fullName: $"Test {email.Split('@')[0]}"));

        var progressUpdates = new List<RTUB.Application.DTOs.EmailSendProgress>();
        var progress = new Progress<RTUB.Application.DTOs.EmailSendProgress>(update =>
        {
            progressUpdates.Add(update);
        });

        // Setup SMTP configuration
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPort"]).Returns("587");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpUsername"]).Returns("test@test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderName"]).Returns("RTUB");

        // Act - method should accept progress parameter without errors
        var result = await _service.SendEventNotificationAsync(
            eventId: 1,
            eventTitle: "Test Event",
            eventDate: DateTime.Now.AddDays(7),
            eventLocation: "Coimbra",
            eventLink: "https://rtub.pt/events/1",
            recipientEmails: recipientEmails,
            recipientData: recipientData,
            eventDescription: "Test",
            endDate: null,
            progress: progress);

        // Assert - method completes without throwing, even if actual sending fails
        result.Should().NotBeNull();
        // Note: Progress may not be reported in unit tests without real SMTP
    }

    [Fact]
    public async Task SendBirthdayNotificationAsync_WithProgressCallback_AcceptsProgressParameter()
    {
        // Arrange
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com" };
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email.Split('@')[0]}", fullName: $"Test {email.Split('@')[0]}"));

        var progressUpdates = new List<RTUB.Application.DTOs.EmailSendProgress>();
        var progress = new Progress<RTUB.Application.DTOs.EmailSendProgress>(update =>
        {
            progressUpdates.Add(update);
        });

        // Setup SMTP configuration
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPort"]).Returns("587");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpUsername"]).Returns("test@test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderName"]).Returns("RTUB");

        _mockTemplateRenderer.Setup(x => x.RenderBirthdayNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test birthday email");

        // Act - method should accept progress parameter without errors
        var result = await _service.SendBirthdayNotificationAsync(
            birthdayPersonId: "user123",
            birthdayPersonNickname: "Jeans",
            birthdayPersonFullName: "João Silva",
            recipientEmails: recipientEmails,
            recipientData: recipientData,
            progress: progress);

        // Assert - method completes without throwing
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMeetingNotificationAsync_WithProgressCallback_AcceptsProgressParameter()
    {
        // Arrange
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" };
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email.Split('@')[0]}", fullName: $"Test {email.Split('@')[0]}"));

        var progressUpdates = new List<RTUB.Application.DTOs.EmailSendProgress>();
        var progress = new Progress<RTUB.Application.DTOs.EmailSendProgress>(update =>
        {
            progressUpdates.Add(update);
        });

        // Setup SMTP configuration
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPort"]).Returns("587");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpUsername"]).Returns("test@test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderName"]).Returns("RTUB");

        // Act - method should accept progress parameter without errors
        var result = await _service.SendMeetingNotificationAsync(
            meetingId: 1,
            subject: "Test Meeting",
            body: "<html><body>Test meeting body</body></html>",
            recipientEmails: recipientEmails,
            recipientData: recipientData,
            progress: progress);

        // Assert - method completes without throwing
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAnnouncementEmailAsync_WithProgressCallback_AcceptsProgressParameter()
    {
        // Arrange
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com" };
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email.Split('@')[0]}", fullName: $"Test {email.Split('@')[0]}"));

        var progressUpdates = new List<RTUB.Application.DTOs.EmailSendProgress>();
        var progress = new Progress<RTUB.Application.DTOs.EmailSendProgress>(update =>
        {
            progressUpdates.Add(update);
        });

        // Setup SMTP configuration
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPort"]).Returns("587");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpUsername"]).Returns("test@test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderName"]).Returns("RTUB");

        _mockTemplateRenderer.Setup(x => x.RenderAnnouncementEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test announcement email");

        // Act - method should accept progress parameter without errors
        var result = await _service.SendAnnouncementEmailAsync(
            title: "Test Announcement",
            content: "This is a test announcement",
            recipientEmails: recipientEmails,
            recipientData: recipientData,
            progress: progress);

        // Assert - method completes without throwing
        result.Should().NotBeNull();
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
