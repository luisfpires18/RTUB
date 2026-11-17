using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RehearsalAttendanceService
/// Tests business logic and service layer operations
/// </summary>
public partial class RehearsalAttendanceServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly RehearsalAttendanceService _attendanceService;
    private readonly Mock<IMemberStatusService> _mockRetirementStatusService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public RehearsalAttendanceServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockRetirementStatusService = new Mock<IMemberStatusService>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockUserManager = MockHelpers.CreateMockUserManager();

        _attendanceService = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            _mockUserManager.Object,
            Mock.Of<ILogger<RehearsalAttendanceService>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
