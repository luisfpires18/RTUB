using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

public partial class PushNotificationServiceTests
{
    private readonly Mock<IPushSubscriptionRepository> _mockRepository;
    private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
    private readonly Mock<ILogger<PushNotificationService>> _mockLogger;
    private readonly WebPushOptions _options;
    private readonly PushNotificationService _service;

    public PushNotificationServiceTests()
    {
        _mockRepository = new Mock<IPushSubscriptionRepository>();
        _mockUserProfileRepository = new Mock<IUserProfileRepository>();
        _mockLogger = new Mock<ILogger<PushNotificationService>>();

        // Configure with minimal VAPID configuration for testing
        // Note: Keys don't need to be cryptographically valid or functional with actual push services
        // since we're testing business logic, not the actual push notification sending
        _options = new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:test@example.com",
            VapidPublicKey = "test-public-key",
            VapidPrivateKey = "test-private-key"
        };

        var optionsWrapper = Options.Create(_options);
        _service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            optionsWrapper,
            _mockLogger.Object);
    }
}
