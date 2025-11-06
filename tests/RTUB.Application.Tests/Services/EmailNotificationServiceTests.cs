using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class EmailNotificationServiceTests : IDisposable
{
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly IMemoryCache _cache;
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<EmailNotificationService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        // Setup default configuration (SMTP not configured)
        _mockConfiguration.Setup(x => x["EmailSettings:RecipientEmail"]).Returns("jeans@rtub.pt");
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(x => x["EmailSettings:SmtpPassword"]).Returns((string?)null);

        _service = new EmailNotificationService(_mockLogger.Object, _mockConfiguration.Object, _cache);
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
        var password = "TempPassword123";

        // Act
        Func<Task> act = async () => await _service.SendWelcomeEmailAsync(userName, email, firstName, password);

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

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
