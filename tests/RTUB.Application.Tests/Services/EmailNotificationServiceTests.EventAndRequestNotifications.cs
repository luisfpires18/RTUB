using FluentAssertions;
using Moq;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;

namespace RTUB.Application.Tests.Services;

// EmailNotificationServiceTests: Event And Request Notifications tests.
public partial class EmailNotificationServiceTests
{
    [Fact]
    public async Task SendEventNotificationAsync_WithRateLimit_ReturnsEarly()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SenderEmail = "sender@test.com",
            SenderName = "Test Sender",
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "user",
            SmtpPassword = "pass"
        };

        var service = CreateService(config);

        // First call - should succeed (or attempt to)
        var recipientEmails = new List<string> { "user1@test.com" };
        var recipientData = new Dictionary<string, (string nickname, string fullName)>
        {
            { "user1@test.com", ("Nick1", "Full Name 1") }
        };

        _mockTemplateRenderer.Setup(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Email body");

        // First call
        await service.SendEventNotificationAsync(
            eventId: 1,
            eventTitle: "Test Event",
            eventDate: DateTime.Now,
            eventLocation: "Test Location",
            eventLink: "https://test.com/event/1",
            recipientEmails: recipientEmails,
            recipientData: recipientData);

        // Act - Second call immediately after (should be rate limited)
        var result = await service.SendEventNotificationAsync(
            eventId: 1,
            eventTitle: "Test Event",
            eventDate: DateTime.Now,
            eventLocation: "Test Location",
            eventLink: "https://test.com/event/1",
            recipientEmails: recipientEmails,
            recipientData: recipientData);

        // Assert
        result.success.Should().BeFalse("should be rate limited");
        result.errorMessage.Should().Contain("já enviado recentemente");

        // Template should not be rendered again due to rate limiting
        _mockTemplateRenderer.Verify(x => x.RenderEventNotificationAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once, "template should only be rendered once due to rate limiting");
    }

    [Fact]
    public async Task SendNewRequestNotificationAsync_GoesToTheRtubInbox_WithoutEmailSettingsRecipient()
    {
        // EmailSettings:RecipientEmail is deliberately unset (production points it at no-reply):
        // the public request flow must not depend on it.
        var service = CreateService(new EmailConfiguration
        {
            SenderEmail = "sender@test.com",
            SenderName = "Test Sender",
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "user",
            SmtpPassword = "pass"
        });

        await service.SendNewRequestNotificationAsync(42, "Cliente", "cliente@test.com", "912345678",
            "Casamento", DateTime.Today, null, "Braga", "Mensagem", DateTime.Now);

        EmailNotificationService.RequestNotificationRecipient.Should().Be("realtunab@gmail.com");
        _mockTemplateRenderer.Verify(x => x.RenderNewRequestNotificationAsync(
            "Cliente", "cliente@test.com", "912345678", "Casamento", It.IsAny<string>(), "Braga", "Mensagem", It.IsAny<DateTime>()),
            Times.Once, "the email is built even though EmailSettings:RecipientEmail is not configured");
    }
}
