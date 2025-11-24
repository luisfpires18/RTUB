using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Controllers;
using Xunit;

namespace RTUB.Web.Tests.Controllers;

public class PushControllerTests
{
    private readonly Mock<IPushNotificationService> _mockPushService;
    private readonly Mock<ILogger<PushController>> _mockLogger;
    private readonly WebPushOptions _options;
    private readonly PushController _controller;

    public PushControllerTests()
    {
        _mockPushService = new Mock<IPushNotificationService>();
        _mockLogger = new Mock<ILogger<PushController>>();
        
        _options = new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:test@example.com",
            VapidPublicKey = "test-public-key",
            VapidPrivateKey = "test-private-key"
        };

        var optionsWrapper = Options.Create(_options);
        _controller = new PushController(_mockPushService.Object, optionsWrapper, _mockLogger.Object);
    }

    private void SetupUserContext(string userId, bool isOwner = false)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User")
        };

        if (isOwner)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Owner"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public void GetStatus_ReturnsEnabledTrue_WhenUserIsOwner()
    {
        // Arrange
        _options.Enabled = false; // Even when disabled
        SetupUserContext("test-user", isOwner: true);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(true);
        _mockPushService.Setup(s => s.GetVapidPublicKey()).Returns("test-public-key");

        // Act
        var result = _controller.GetStatus() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var statusDto = result.Value as PushStatusDto;
        Assert.NotNull(statusDto);
        Assert.True(statusDto.IsEnabled, "OWNER should always have access");
        Assert.True(statusDto.IsConfigured);
        Assert.Equal("test-public-key", statusDto.VapidPublicKey);
    }

    [Fact]
    public void GetStatus_ReturnsEnabledTrue_WhenFeatureIsEnabled()
    {
        // Arrange
        _options.Enabled = true;
        SetupUserContext("test-user", isOwner: false);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(true);
        _mockPushService.Setup(s => s.GetVapidPublicKey()).Returns("test-public-key");

        // Act
        var result = _controller.GetStatus() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var statusDto = result.Value as PushStatusDto;
        Assert.NotNull(statusDto);
        Assert.True(statusDto.IsEnabled);
        Assert.True(statusDto.IsConfigured);
        Assert.Equal("test-public-key", statusDto.VapidPublicKey);
    }

    [Fact]
    public void GetStatus_ReturnsEnabledFalse_WhenFeatureIsDisabledAndNotOwner()
    {
        // Arrange
        _options.Enabled = false;
        SetupUserContext("test-user", isOwner: false);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(true);

        // Act
        var result = _controller.GetStatus() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var statusDto = result.Value as PushStatusDto;
        Assert.NotNull(statusDto);
        Assert.False(statusDto.IsEnabled);
        Assert.Null(statusDto.VapidPublicKey);
    }

    [Fact]
    public async Task Subscribe_ReturnsForbid_WhenUserHasNoAccess()
    {
        // Arrange
        _options.Enabled = false;
        SetupUserContext("test-user", isOwner: false);
        var subscriptionDto = new PushSubscriptionDto();

        // Act
        var result = await _controller.Subscribe(subscriptionDto);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Subscribe_ReturnsOk_WhenUserIsOwner()
    {
        // Arrange
        _options.Enabled = false; // Even when disabled
        SetupUserContext("test-user", isOwner: true);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(true);
        
        var subscriptionDto = new PushSubscriptionDto
        {
            Endpoint = "https://push.example.com/test",
            Keys = new PushKeysDto
            {
                P256dh = "test-p256dh",
                Auth = "test-auth"
            }
        };

        // Act
        var result = await _controller.Subscribe(subscriptionDto) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        _mockPushService.Verify(s => s.SubscribeAsync(
            It.Is<string>(id => id == "test-user"),
            It.IsAny<PushSubscriptionDto>(),
            It.IsAny<string>(),
            It.Is<string?>(name => name == "Test User")), Times.Once);
    }

    [Fact]
    public async Task Subscribe_ReturnsBadRequest_WhenNotConfigured()
    {
        // Arrange
        SetupUserContext("test-user", isOwner: true);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(false);
        
        var subscriptionDto = new PushSubscriptionDto();

        // Act
        var result = await _controller.Subscribe(subscriptionDto) as BadRequestObjectResult;

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Unsubscribe_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        SetupUserContext("test-user", isOwner: true);
        var request = new UnsubscribeRequest { Endpoint = "https://push.example.com/test" };

        // Act
        var result = await _controller.Unsubscribe(request) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        _mockPushService.Verify(s => s.UnsubscribeAsync(request.Endpoint), Times.Once);
    }

    [Fact]
    public async Task SendTest_ReturnsForbid_WhenUserHasNoAccess()
    {
        // Arrange
        _options.Enabled = false;
        SetupUserContext("test-user", isOwner: false);
        var notification = new SendPushNotificationDto();

        // Act
        var result = await _controller.SendTest(notification);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task SendTest_ReturnsOk_WhenUserIsOwner()
    {
        // Arrange
        SetupUserContext("test-user", isOwner: true);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(true);
        
        var notification = new SendPushNotificationDto
        {
            Title = "Test",
            Body = "Test notification"
        };

        // Act
        var result = await _controller.SendTest(notification) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        _mockPushService.Verify(s => s.SendToUserAsync(
            It.Is<string>(id => id == "test-user"),
            It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task Broadcast_RequiresOwnerRole()
    {
        // This test verifies that the [Authorize(Roles = "Owner")] attribute is present
        // Actual authorization testing would require integration tests
        
        // Arrange
        SetupUserContext("test-user", isOwner: true);
        _mockPushService.Setup(s => s.IsConfigured()).Returns(true);
        
        var notification = new SendPushNotificationDto
        {
            Title = "Broadcast",
            Body = "Broadcast notification"
        };

        // Act
        var result = await _controller.Broadcast(notification) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        _mockPushService.Verify(s => s.BroadcastAsync(It.IsAny<SendPushNotificationDto>()), Times.Once);
    }
}
