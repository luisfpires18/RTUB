using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for concurrent email sending to verify ObjectDisposedException fix
/// </summary>
public class EmailNotificationServiceConcurrencyTests : IDisposable
{
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly Mock<ILogger<EmailConfigurationProvider>> _mockConfigLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly IMemoryCache _cache;
    private readonly Mock<IEmailTemplateRenderer> _mockTemplateRenderer;
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceConcurrencyTests()
    {
        _mockLogger = new Mock<ILogger<EmailNotificationService>>();
        _mockConfigLogger = new Mock<ILogger<EmailConfigurationProvider>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockTemplateRenderer = new Mock<IEmailTemplateRenderer>();

        // Setup SMTP configuration to enable actual sending logic
        _mockConfiguration.Setup(x => x["EmailSettings:RecipientEmail"]).Returns("admin@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns("smtp.test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPort"]).Returns("587");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpUsername"]).Returns("test@test.com");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns("test-password");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderEmail"]).Returns("noreply@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SenderName"]).Returns("RTUB");

        // Setup template renderer to return dummy HTML
        _mockTemplateRenderer.Setup(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string title, DateTime start, DateTime? end, string loc, string link, 
                string nick, string full, string desc) => 
                $"<html><body>Event: {title} for {nick} ({full})</body></html>");
        
        _mockTemplateRenderer.Setup(x => x.RenderBirthdayNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string bdayNick, string bdayFull, string recipNick, string recipFull) => 
                $"<html><body>Birthday: {bdayNick} to {recipNick} ({recipFull})</body></html>");
        
        _mockTemplateRenderer.Setup(x => x.RenderEventCancellationNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string title, string date, string loc, string reason, string link, 
                string nick, string full) => 
                $"<html><body>Cancelled: {title} for {nick} ({full})</body></html>");
        
        _mockTemplateRenderer.Setup(x => x.RenderEventReminderNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string title, DateTime start, DateTime? end, string loc, string link, 
                int days, string nick, string full, string desc) => 
                $"<html><body>Reminder: {title} in {days} days for {nick} ({full})</body></html>");
        
        _mockTemplateRenderer.Setup(x => x.RenderAnnouncementEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string title, string content, string nick, string full) => 
                $"<html><body>Announcement: {title} for {nick} ({full})</body></html>");

        // Create the refactored dependencies
        var configProvider = new EmailConfigurationProvider(_mockConfiguration.Object, _mockConfigLogger.Object);
        var smtpFactory = new SmtpClientFactory();
        var rateLimiter = new EmailRateLimiter(_cache);

        _service = new EmailNotificationService(
            _mockLogger.Object,
            configProvider,
            smtpFactory,
            rateLimiter,
            _mockTemplateRenderer.Object);
    }

    [Fact]
    public async Task SendEventNotificationAsync_WithManyRecipients_DoesNotThrowObjectDisposedException()
    {
        // Arrange - simulate ~66 users (problem statement mentions up to 150, using 66 for faster test)
        var recipientEmails = Enumerable.Range(1, 66)
            .Select(i => $"user{i}@test.com")
            .ToList();
        
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email[4]}", fullName: $"Test User {email[4]}"));

        var eventId = 100;
        var eventTitle = "Large Event";
        var eventDate = DateTime.Now.AddDays(7);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events/100";

        // Act - this should not throw ObjectDisposedException with the fix
        Func<Task> act = async () => await _service.SendEventNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, 
            recipientEmails, recipientData);

        // Assert - should complete without ObjectDisposedException
        await act.Should().NotThrowAsync<ObjectDisposedException>();
        
        // Verify template renderer was called for each recipient
        _mockTemplateRenderer.Verify(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Exactly(66));
    }

    [Fact]
    public async Task SendBirthdayNotificationAsync_WithManyRecipients_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        var recipientEmails = Enumerable.Range(1, 50)
            .Select(i => $"user{i}@test.com")
            .ToList();
        
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email[4]}", fullName: $"Test User {email[4]}"));

        var birthdayPersonId = "birthday-user-1";
        var birthdayPersonNickname = "Jeans";
        var birthdayPersonFullName = "João Silva";

        // Act
        Func<Task> act = async () => await _service.SendBirthdayNotificationAsync(
            birthdayPersonId, birthdayPersonNickname, birthdayPersonFullName, 
            recipientEmails, recipientData);

        // Assert
        await act.Should().NotThrowAsync<ObjectDisposedException>();
        
        // Verify template renderer was called for each recipient
        _mockTemplateRenderer.Verify(x => x.RenderBirthdayNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Exactly(50));
    }

    [Fact]
    public async Task SendEventCancellationNotificationAsync_WithManyRecipients_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        var recipientEmails = Enumerable.Range(1, 75)
            .Select(i => $"user{i}@test.com")
            .ToList();
        
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email[4]}", fullName: $"Test User {email[4]}"));

        var eventId = 200;
        var eventTitle = "Cancelled Event";
        var eventDate = DateTime.Now.AddDays(3);
        var eventLocation = "Lisboa";
        var cancellationReason = "Mau tempo";
        var eventLink = "https://rtub.azurewebsites.net/events/200";

        // Act
        Func<Task> act = async () => await _service.SendEventCancellationNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, cancellationReason, eventLink,
            recipientEmails, recipientData);

        // Assert
        await act.Should().NotThrowAsync<ObjectDisposedException>();
        
        // Verify template renderer was called for each recipient
        _mockTemplateRenderer.Verify(x => x.RenderEventCancellationNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Exactly(75));
    }

    [Fact]
    public async Task SendEventReminderNotificationAsync_WithManyRecipients_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        var recipientEmails = Enumerable.Range(1, 60)
            .Select(i => $"user{i}@test.com")
            .ToList();
        
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email[4]}", fullName: $"Test User {email[4]}"));

        var eventId = 300;
        var eventTitle = "Upcoming Event";
        var eventDate = DateTime.Now.AddDays(2);
        var eventLocation = "Porto";
        var eventLink = "https://rtub.azurewebsites.net/events/300";

        // Act
        Func<Task> act = async () => await _service.SendEventReminderNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, 
            recipientEmails, recipientData);

        // Assert
        await act.Should().NotThrowAsync<ObjectDisposedException>();
        
        // Verify template renderer was called for each recipient
        _mockTemplateRenderer.Verify(x => x.RenderEventReminderNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Exactly(60));
    }

    [Fact]
    public async Task SendAnnouncementEmailAsync_WithManyRecipients_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        var recipientEmails = Enumerable.Range(1, 80)
            .Select(i => $"user{i}@test.com")
            .ToList();
        
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email[4]}", fullName: $"Test User {email[4]}"));

        var title = "Important Announcement";
        var content = "This is an important announcement for all members.";

        // Act
        Func<Task> act = async () => await _service.SendAnnouncementEmailAsync(
            title, content, recipientEmails, recipientData);

        // Assert
        await act.Should().NotThrowAsync<ObjectDisposedException>();
        
        // Verify template renderer was called for each recipient
        _mockTemplateRenderer.Verify(x => x.RenderAnnouncementEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Exactly(80));
    }

    [Fact]
    public async Task SendPersonalizedBatchAsync_AttemptsAllRecipientsEvenWithFailures()
    {
        // Arrange
        var recipientEmails = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" };
        var recipientData = recipientEmails.ToDictionary(
            email => email,
            email => (nickname: $"User{email[4]}", fullName: $"Test User {email[4]}"));

        // Make the template renderer throw for user2 only
        _mockTemplateRenderer.Setup(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), 
            It.IsAny<string>(), "User2", It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Simulated error for user2"));
        
        var eventId = 400;
        var eventTitle = "Test Event";
        var eventDate = DateTime.Now.AddDays(5);
        var eventLocation = "Coimbra";
        var eventLink = "https://rtub.azurewebsites.net/events/400";

        // Act
        var result = await _service.SendEventNotificationAsync(
            eventId, eventTitle, eventDate, eventLocation, eventLink, 
            recipientEmails, recipientData);

        // Assert - all recipients should be attempted despite individual failures
        // Verify that all recipients were attempted (template render called for all)
        _mockTemplateRenderer.Verify(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Exactly(3), "All recipients should be attempted even if some fail");
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
