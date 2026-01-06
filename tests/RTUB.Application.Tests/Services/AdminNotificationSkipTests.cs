using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests to verify that push notifications are skipped when admins manually add users
/// </summary>
public class AdminNotificationSkipTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EnrollmentService _enrollmentService;
    private readonly RehearsalAttendanceService _attendanceService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IRetirementStatusService> _mockRetirementStatusService;
    private readonly Mock<IMemberStatusService> _mockMemberStatusService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public AdminNotificationSkipTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext());

        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockRetirementStatusService = new Mock<IRetirementStatusService>();
        _mockMemberStatusService = new Mock<IMemberStatusService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockUserManager = MockHelpers.CreateMockUserManager();

        _enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            _mockMemberStatusService.Object,
            _mockPushNotificationFactory.Object,
            _mockPushNotificationService.Object,
            _mockHttpContextAccessor.Object);

        _attendanceService = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(_context),
            _mockRetirementStatusService.Object,
            _mockMemberStatusService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            _mockHttpContextAccessor.Object,
            _mockUserManager.Object);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithSkipNotification_DoesNotSendNotifications()
    {
        // Arrange
        var eventEntity = Core.Entities.Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival, "Description");
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act - Admin adds a user with skipNotification = true
        await _enrollmentService.CreateEnrollmentAsync(
            "admin-added-user",
            eventEntity.Id,
            InstrumentType.Guitarra,
            null,
            true,
            null,
            skipNotification: true);

        // Assert - Verify notification service was not called
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DTOs.SendPushNotificationDto>()),
            Times.Never,
            "Notifications should not be sent when skipNotification is true");
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithoutSkipNotification_SendsNotifications()
    {
        // Arrange
        var eventEntity = Core.Entities.Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival, "Description");
        _context.Events.Add(eventEntity);
        
        // Add an existing enrolled user so there's someone to notify
        var existingUser = new ApplicationUser
        {
            Id = "existing-user-id",
            UserName = "existing@test.com",
            Email = "existing@test.com",
            FirstName = "Existing",
            LastName = "User",
            Nickname = "ExistingUser"
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();
        
        var existingEnrollment = Enrollment.Create(existingUser.Id, eventEntity.Id);
        existingEnrollment.WillAttend = true;
        _context.Enrollments.Add(existingEnrollment);
        await _context.SaveChangesAsync();

        // Setup mock to allow notification calls
        _mockHttpContextAccessor.Setup(x => x.HttpContext!.Request.Scheme).Returns("https");
        _mockHttpContextAccessor.Setup(x => x.HttpContext!.Request.Host).Returns(new HostString("test.com"));

        // Act - User self-enrolls without skipNotification parameter (default false)
        var newUser = new ApplicationUser
        {
            Id = "new-user-id",
            UserName = "new@test.com",
            Email = "new@test.com",
            FirstName = "New",
            LastName = "User",
            Nickname = "NewUser"
        };
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        await _enrollmentService.CreateEnrollmentAsync(
            newUser.Id,
            eventEntity.Id,
            InstrumentType.Guitarra,
            null,
            true,
            null,
            skipNotification: false);

        // Assert - Verify notification service was called
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DTOs.SendPushNotificationDto>()),
            Times.Once,
            "Notifications should be sent when skipNotification is false and there are users to notify");
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithSkipNotification_DoesNotSendNotifications()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act - Admin adds a user with skipNotification = true
        await _attendanceService.MarkAttendanceAsync(
            rehearsal.Id,
            "admin-added-user",
            true,
            InstrumentType.Guitarra,
            null,
            null,
            skipNotification: true);

        // Assert - Verify notification service was not called
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DTOs.SendPushNotificationDto>()),
            Times.Never,
            "Notifications should not be sent when skipNotification is true");
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithoutSkipNotification_SendsNotifications()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        
        // Add an existing attendee so there's someone to notify
        var existingUser = new ApplicationUser
        {
            Id = "existing-user-id",
            UserName = "existing@test.com",
            Email = "existing@test.com",
            FirstName = "Existing",
            LastName = "User",
            Nickname = "ExistingUser"
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();
        
        var existingAttendance = RehearsalAttendance.Create(rehearsal.Id, existingUser.Id, InstrumentType.Bandolim);
        existingAttendance.WillAttend = true;
        _context.RehearsalAttendances.Add(existingAttendance);
        await _context.SaveChangesAsync();

        // Setup mock to allow notification calls
        _mockHttpContextAccessor.Setup(x => x.HttpContext!.Request.Scheme).Returns("https");
        _mockHttpContextAccessor.Setup(x => x.HttpContext!.Request.Host).Returns(new HostString("test.com"));

        // Act - User marks their own attendance without skipNotification parameter (default false)
        var newUser = new ApplicationUser
        {
            Id = "new-user-id",
            UserName = "new@test.com",
            Email = "new@test.com",
            FirstName = "New",
            LastName = "User",
            Nickname = "NewUser"
        };
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        await _attendanceService.MarkAttendanceAsync(
            rehearsal.Id,
            newUser.Id,
            true,
            InstrumentType.Guitarra,
            null,
            null,
            skipNotification: false);

        // Assert - Verify notification service was called
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DTOs.SendPushNotificationDto>()),
            Times.Once,
            "Notifications should be sent when skipNotification is false and there are users to notify");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
