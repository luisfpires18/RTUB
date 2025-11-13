using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class EmailNotificationServiceTests : IDisposable
{
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly IMemoryCache _cache;
    private readonly Mock<IEmailTemplateRenderer> _mockTemplateRenderer;
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<EmailNotificationService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockTemplateRenderer = new Mock<IEmailTemplateRenderer>();

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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test event email");
        
        _mockTemplateRenderer.Setup(x => x.RenderEventCancellationNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test cancellation email");

        _service = new EmailNotificationService(_mockLogger.Object, _mockConfiguration.Object, _cache, _mockTemplateRenderer.Object);
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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
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

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
