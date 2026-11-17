using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for admin-specific rehearsal attendance workflows
/// Tests the workflow of admins adding members directly to rehearsal attendance
/// </summary>
public partial class RehearsalAttendanceAdminWorkflowTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    private readonly RehearsalAttendanceService _attendanceService;
    private readonly Mock<IMemberStatusService> _mockRetirementStatusService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public RehearsalAttendanceAdminWorkflowTests()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(_dbOptions, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext(), new RTUB.Application.Services.AuditLogAppender());

        _mockRetirementStatusService = new Mock<IMemberStatusService>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockUserManager = MockHelpers.CreateMockUserManager();

        _attendanceService = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(CreateContextFactory()),
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

    private IDbContextFactory<ApplicationDbContext> CreateContextFactory()
    {
        var mock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mock.Setup(f => f.CreateDbContext()).Returns(() =>
            new ApplicationDbContext(_dbOptions, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender()));
        return mock.Object;
    }
}
