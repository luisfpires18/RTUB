using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingRequestService
/// </summary>
public partial class MeetingRequestServiceTests
{
    private readonly Mock<IMeetingRequestRepository> _repositoryMock;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<MeetingRequestService>> _mockLogger;
    private readonly MeetingRequestService _service;

    public MeetingRequestServiceTests()
    {
        _repositoryMock = new Mock<IMeetingRequestRepository>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockUserManager = MockHelpers.CreateMockUserManager();
        _mockLogger = new Mock<ILogger<MeetingRequestService>>();

        _service = new MeetingRequestService(
            _repositoryMock.Object,
            _mockPushNotificationFactory.Object,
            _mockPushNotificationService.Object,
            _mockUserManager.Object,
            _mockLogger.Object);
    }
}
