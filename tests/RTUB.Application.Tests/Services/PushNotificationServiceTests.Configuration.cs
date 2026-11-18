// PushNotificationService: configuration.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

public partial class PushNotificationServiceTests
{
    [Fact]
    public void IsConfigured_ReturnsTrueWhenProperlyConfigured()
    {
        // Act
        var result = _service.IsConfigured();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConfigured_ReturnsFalseWhenNotConfigured()
    {
        // Arrange
        var emptyOptions = Options.Create(new WebPushOptions());
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            emptyOptions,
            _mockLogger.Object);

        // Act
        var result = service.IsConfigured();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetVapidPublicKey_ReturnsPublicKey()
    {
        // Act
        var result = _service.GetVapidPublicKey();

        // Assert
        Assert.Equal(_options.VapidPublicKey, result);
    }

    [Theory]
    [InlineData("abc+def/ghi=", "abc-def_ghi")]
    [InlineData("abc-def_ghi", "abc-def_ghi")]
    [InlineData("AQID", "AQID")]
    [InlineData("AQ==", "AQ")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void NormalizeBase64Url_ConvertsCorrectly(string? input, string? expected)
    {
        var result = PushNotificationService.NormalizeBase64Url(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Constructor_AcceptsRealVapidKeys_WithoutLoggingInvalidConfigurationWarning()
    {
        // Regression guard for the WebPush package migration (phase 009).
        // PushNotificationService swallows ArgumentException from SetVapidDetails, so a library
        // that rejects RTUB's real VAPID key format would leave IsConfigured() == true while
        // never being able to send. The rest of this class uses placeholder keys, which always
        // take the swallowed path, so only a real keypair exercises this.
        // Keys are generated per-run - nothing key-shaped is stored in the repository.
        var vapidKeys = WebPush.VapidHelper.GenerateVapidKeys();

        // Fresh logger: the shared _mockLogger already recorded the warning from the
        // placeholder-key service built in this class's constructor.
        var logger = new Mock<ILogger<PushNotificationService>>();

        var options = Options.Create(new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:test@example.com",
            VapidPublicKey = vapidKeys.PublicKey,
            VapidPrivateKey = vapidKeys.PrivateKey
        });

        // Act
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            options,
            logger.Object);

        // Assert
        Assert.True(service.IsConfigured());
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
