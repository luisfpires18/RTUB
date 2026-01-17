using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberStatusService
/// Tests business logic for retirement status calculation and last activity tracking
/// Now includes tests for database-backed caching and audit logging
/// </summary>
public class MemberStatusServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MemberStatusService _service;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ILogger<MemberStatusService>> _mockLogger;
    private readonly Mock<IOptions<MemberStatusUpdateOptions>> _mockOptions;

    public MemberStatusServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();

        // Create mock UserManager
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Create mock push notification service
        _mockPushNotificationService = new Mock<IPushNotificationService>();

        // Create mock audit log service
        _mockAuditLogService = new Mock<IAuditLogService>();

        // Create mock logger
        _mockLogger = new Mock<ILogger<MemberStatusService>>();

        // Create mock options with push notifications disabled for tests
        _mockOptions = new Mock<IOptions<MemberStatusUpdateOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(new MemberStatusUpdateOptions
        {
            Enabled = true,
            ScheduledTime = "21:00",
            PushNotificationsEnabled = false
        });

        _service = new MemberStatusService(
            _context,
            _mockUserManager.Object,
            _mockPushNotificationService.Object,
            _mockAuditLogService.Object,
            _mockLogger.Object,
            _mockOptions.Object);
    }

    [Fact]
    public async Task GetMemberStatusAsync_WithNoActivity_ReturnsNoActivityAndNotRetired()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeFalse();
        result.LastRehearsalDate.Should().BeNull();
        result.LastEventDate.Should().BeNull();
        result.LastActivityDate.Should().BeNull();
        result.IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task GetMemberStatusAsync_WithOnlyFutureRehearsals_ReturnsNoActivity()
    {
        // Arrange - CRITICAL BUG FIX TEST
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Create future rehearsals (should be excluded)
        var futureDate1 = DateTime.UtcNow.AddDays(10);
        var futureDate2 = DateTime.UtcNow.AddMonths(2);

        var rehearsal1 = Rehearsal.Create(futureDate1, "Location 1");
        var rehearsal2 = Rehearsal.Create(futureDate2, "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);

        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attendance2.MarkAttendance(true);

        _context.RehearsalAttendances.AddRange(attendance1, attendance2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Future activities should NOT be counted
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeFalse();
        result.LastRehearsalDate.Should().BeNull();
        result.LastEventDate.Should().BeNull();
        result.LastActivityDate.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberStatusAsync_WithOnlyFutureEvents_ReturnsNoActivity()
    {
        // Arrange - CRITICAL BUG FIX TEST
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Create future events (should be excluded)
        var futureDate1 = DateTime.UtcNow.AddDays(10);
        var futureDate2 = DateTime.UtcNow.AddMonths(3);

        var event1 = Event.Create("Event 1", futureDate1, "Location 1", EventType.Festival);
        var event2 = Event.Create("Event 2", futureDate2, "Location 2", EventType.Atuacao);
        _context.Events.AddRange(event1, event2);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;

        var enrollment2 = Enrollment.Create(userId, event2.Id);
        enrollment2.WillAttend = true;

        _context.Enrollments.AddRange(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Future events should NOT be counted
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeFalse();
        result.LastRehearsalDate.Should().BeNull();
        result.LastEventDate.Should().BeNull();
        result.LastActivityDate.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberStatusAsync_RetiredMemberWith3ConsecutiveMonthsOfActivity_BecomesActive()
    {
        // Arrange - Test that retired member returns to active after 3 consecutive months
        // Current month is included if activities are in the PAST
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Manually set as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Create activities in the last 3 months including current month
        // Use dates in the past to ensure they are counted
        var now = DateTime.UtcNow;

        // Current month - 1 day ago to ensure it's in the past
        var oneDayAgo = now.AddDays(-1);
        var rehearsalCurrent = Rehearsal.Create(oneDayAgo, "Location Current");
        _context.Rehearsals.Add(rehearsalCurrent);
        await _context.SaveChangesAsync();

        var attendanceCurrent = RehearsalAttendance.Create(rehearsalCurrent.Id, userId);
        attendanceCurrent.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendanceCurrent);

        // 1 month ago - mid-month to ensure it's in the correct month
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // 2 months ago - mid-month
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var event1 = Event.Create("Event 1", twoMonthsAgo, "Location 2", EventType.Atuacao);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;
        _context.Enrollments.Add(enrollment1);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should become active (not retired) after 3 consecutive months
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeFalse(); // Should be automatically set to active

        // Verify that user was updated in database
        _mockUserManager.Verify(um => um.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.Id == userId && u.IsRetired == false)), Times.Once);
    }

    [Fact]
    public async Task GetMemberStatusAsync_RetiredMemberWith2ConsecutiveMonths_StaysRetired()
    {
        // Arrange - Test that retired member needs 3 consecutive months to become active
        // Current month is included if it has past activities
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Manually set as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Current month - 1 day ago
        var oneDayAgo = now.AddDays(-1);
        var rehearsalCurrent = Rehearsal.Create(oneDayAgo, "Location Current");
        _context.Rehearsals.Add(rehearsalCurrent);
        await _context.SaveChangesAsync();

        var attendanceCurrent = RehearsalAttendance.Create(rehearsalCurrent.Id, userId);
        attendanceCurrent.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendanceCurrent);

        // 1 month ago - mid-month
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // No activity 2 months ago - breaks the consecutive chain at 2 months

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should stay retired (only 2 consecutive months)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue();
        result.ProgressDescription.Should().Contain("2/3 meses de atividade consecutiva");
    }

    [Fact]
    public async Task GetMemberStatusAsync_FutureActivitiesDoNotCountTowardConsecutiveMonths()
    {
        // Arrange - Test that future activities are excluded from consecutive months calculation
        // NOTE: Current month is also excluded
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Current month - 1 day ago (past activity)
        var oneDayAgo = now.AddDays(-1);
        var rehearsal0 = Rehearsal.Create(oneDayAgo, "Location Current");
        _context.Rehearsals.Add(rehearsal0);
        await _context.SaveChangesAsync();

        var attendance0 = RehearsalAttendance.Create(rehearsal0.Id, userId);
        attendance0.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance0);

        // 1 month ago - mid-month
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // No activity 2 months ago - this breaks the consecutive chain at 2 months

        // Future activity (should NOT count) - use a date clearly in the future
        var futureDate = now.AddDays(10);
        var rehearsal2 = Rehearsal.Create(futureDate, "Location Future");
        _context.Rehearsals.Add(rehearsal2);
        await _context.SaveChangesAsync();

        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attendance2.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance2);

        // Another future activity even further out
        var futureDate2 = now.AddMonths(1);
        var rehearsal3 = Rehearsal.Create(futureDate2, "Location Future 2");
        _context.Rehearsals.Add(rehearsal3);
        await _context.SaveChangesAsync();

        var attendance3 = RehearsalAttendance.Create(rehearsal3.Id, userId);
        attendance3.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance3);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should count only 2 consecutive months (1 and 2 months ago), excluding future activities
        result.Should().NotBeNull();
        result.IsRetired.Should().BeTrue();
        result.ProgressDescription.Should().Contain("2/3 meses de atividade consecutiva");
    }

    [Fact]
    public async Task GetMemberStatusAsync_MultiDayEventCountedInStartMonth()
    {
        // Arrange - Test that multi-day events are counted in the month they START, not END
        // NOTE: Current month is excluded from count
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var now = DateTime.UtcNow;

        // Current month - 1 day ago
        var oneDayAgo = now.AddDays(-1);
        var rehearsal0 = Rehearsal.Create(oneDayAgo, "Location Current");
        _context.Rehearsals.Add(rehearsal0);
        await _context.SaveChangesAsync();

        var attendance0 = RehearsalAttendance.Create(rehearsal0.Id, userId);
        attendance0.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance0);

        // 1 month ago - mid-month regular rehearsal
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // 2 months ago - MULTI-DAY event that starts in month -2 but ends in month -1
        // This should be counted in the START month (2 months ago), not the END month (1 month ago)
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 28); // Near end of month
        var oneMonthAgoEnd = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 5); // Early in next month
        var event1 = Event.Create("Multi-day Event", twoMonthsAgo, "Location 1", EventType.Festival);
        event1.EndDate = oneMonthAgoEnd; // Event spans two months
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;
        _context.Enrollments.Add(enrollment1);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should count 3 consecutive months (multi-day event counted in START month)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeFalse(); // Should transition to active with 3 consecutive months

        // Verify that user was updated in database
        _mockUserManager.Verify(um => um.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.Id == userId && u.IsRetired == false)), Times.Once);
    }

    [Fact]
    public async Task GetMemberStatusAsync_UsesCachedResult_WhenFresh()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity 2 months ago so recalculation produces ProgressMonths = 4
        // 1 completed month without activity + 1 (no current month) = 2
        // ProgressMonths = 6 - 2 = 4
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var rehearsal = Rehearsal.Create(twoMonthsAgo, "Old Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Create a fresh cached status (less than 1 hour old)
        var cachedStatus = new MemberStatus
        {
            UserId = userId,
            IsRetired = false,
            HasAnyActivity = true,
            LastRehearsalDate = twoMonthsAgo,
            LastEventDate = null,
            LastActivityDate = twoMonthsAgo,
            ProgressMonths = 4, // This will be recalculated dynamically
            ProgressTotalMonths = 6,
            ProgressDescription = "4 meses até reforma",
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-30), // 30 minutes ago (fresh)
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.MemberStatuses.Add(cachedStatus);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Progress is dynamically recalculated even for fresh cache
        // 1 completed month without activity (last month) + 1 (no current month) = 2
        // ProgressMonths = 6 - 2 = 4
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse();
        result.HasAnyActivity.Should().BeTrue();
        result.ProgressMonths.Should().Be(4);
        result.ProgressTotalMonths.Should().Be(6);
    }

    [Fact]
    public async Task GetMemberStatusAsync_RecalculatesStatus_WhenStale()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Create a stale cached status (older than 1 hour)
        var cachedStatus = new MemberStatus
        {
            UserId = userId,
            IsRetired = true, // Stale data says retired
            HasAnyActivity = true,
            LastActivityDate = DateTime.UtcNow.AddDays(-10),
            LastUpdatedAt = DateTime.UtcNow.AddHours(-2), // 2 hours ago (stale)
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.MemberStatuses.Add(cachedStatus);
        await _context.SaveChangesAsync();

        // Add recent activity to make user active
        var recentRehearsal = Rehearsal.Create(DateTime.UtcNow.AddDays(-2), "Test Location");
        _context.Rehearsals.Add(recentRehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(recentRehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should recalculate and update
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();

        // Verify the cached status was updated in database
        var updatedCache = await _context.MemberStatuses.FirstOrDefaultAsync(ms => ms.UserId == userId);
        updatedCache.Should().NotBeNull();
        updatedCache!.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateMemberStatusAsync_PersistsToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Add activity
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.AddDays(-10), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateMemberStatusAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();

        // Verify status was persisted to database
        var savedStatus = await _context.MemberStatuses.FirstOrDefaultAsync(ms => ms.UserId == userId);
        savedStatus.Should().NotBeNull();
        savedStatus!.UserId.Should().Be(userId);
        savedStatus.HasAnyActivity.Should().BeTrue();
        savedStatus.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateMemberStatusAsync_SendsNotification_WhenBecomeActive()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        user.Nickname = "TestTuno";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Create existing status showing retired
        var existingStatus = new MemberStatus
        {
            UserId = userId,
            IsRetired = true,
            HasAnyActivity = true,
            LastUpdatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        _context.MemberStatuses.Add(existingStatus);
        await _context.SaveChangesAsync();

        // Add 3 consecutive months of activity to trigger activation
        // Note: The new logic requires 3 completed months, so we need activities 1, 2, and 3 months ago
        var now = DateTime.UtcNow;
        for (int i = 1; i <= 3; i++)
        {
            var rehearsalDate = new DateTime(now.AddMonths(-i).Year, now.AddMonths(-i).Month, 15); // Mid-month
            var rehearsal = Rehearsal.Create(rehearsalDate, $"Location {i}");
            _context.Rehearsals.Add(rehearsal);
            await _context.SaveChangesAsync();

            var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
            attendance.MarkAttendance(true);
            _context.RehearsalAttendances.Add(attendance);
        }
        await _context.SaveChangesAsync();

        // Create a service with push notifications enabled for this test
        var optionsWithNotifications = new Mock<IOptions<MemberStatusUpdateOptions>>();
        optionsWithNotifications.Setup(o => o.Value).Returns(new MemberStatusUpdateOptions
        {
            Enabled = true,
            ScheduledTime = "21:00",
            PushNotificationsEnabled = true
        });

        var serviceWithNotifications = new MemberStatusService(
            _context,
            _mockUserManager.Object,
            _mockPushNotificationService.Object,
            _mockAuditLogService.Object,
            _mockLogger.Object,
            optionsWithNotifications.Object);

        // Act
        var result = await serviceWithNotifications.UpdateMemberStatusAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse(); // Should transition to active

        // Verify push notification was sent (broadcast to all users)
        _mockPushNotificationService.Verify(
            pns => pns.BroadcastAsync(It.Is<RTUB.Application.DTOs.SendPushNotificationDto>(
                dto => dto.Title.Contains("Reativado") && dto.Body.Contains("TestTuno"))),
            Times.Once);
    }

    [Fact]
    public async Task GetMemberStatusAsync_ActiveMemberWith6MonthsInactivity_BecomesRetired()
    {
        // Arrange - Test that active member becomes retired after 6 consecutive months without activity
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = false; // Start as active
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var now = DateTime.UtcNow;

        // Add activity 7 months ago (before the 6-month inactivity window)
        var sevenMonthsAgo = new DateTime(now.AddMonths(-7).Year, now.AddMonths(-7).Month, 15);
        var rehearsal = Rehearsal.Create(sevenMonthsAgo, "Old Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should become retired (6 consecutive months without activity)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue();
    }

    [Fact]
    public async Task GetMemberStatusAsync_ActiveMemberWith5MonthsInactivity_StaysActive()
    {
        // Arrange - Test that active member stays active with only 5 months inactivity
        // With new logic: if no activity in current month, we add 1 to the count for warning purposes
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = false; // Start as active
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity 6 months ago (on the edge, so only 5 completed months without activity)
        // No activity in current month → 5 + 1 = 6 counted → ProgressMonths = 0 ("Próximo da reforma")
        var sixMonthsAgo = new DateTime(now.AddMonths(-6).Year, now.AddMonths(-6).Month, 15);
        var rehearsal = Rehearsal.Create(sixMonthsAgo, "Old Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should stay active (only 5 COMPLETED months without activity, won't become retired until 6 COMPLETED months)
        // But progress shows 0 because: 5 completed + 1 (no current month activity) = 6 → 6 - 6 = 0
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeFalse();
        result.ProgressMonths.Should().Be(0); // 6 - (5 + 1) = 0 months until reform (they're at risk!)
        result.ProgressDescription.Should().Be("Próximo da reforma");
    }

    [Fact]
    public async Task GetMemberStatusAsync_ActiveMemberWithCurrentMonthActivity_Shows6MonthsUntilReform()
    {
        // Arrange - Test that active member with current month activity AND previous month activity shows 6 months until reform
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = false; // Start as active
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity in current month (yesterday)
        var yesterday = now.AddDays(-1);
        var rehearsal1 = Rehearsal.Create(yesterday, "Recent Rehearsal");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // Also add activity in the last completed month (to ensure consecutive activity)
        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal2 = Rehearsal.Create(lastMonth, "Last Month Rehearsal");
        _context.Rehearsals.Add(rehearsal2);
        await _context.SaveChangesAsync();

        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attendance2.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance2);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should show 6 months until reform (0 consecutive completed months without activity)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeFalse();
        result.ProgressMonths.Should().Be(6); // No completed months without activity = 6 months until reform
    }

    [Fact]
    public async Task GetMemberStatusAsync_RetiredMemberWith1MonthActivity_Shows1Of3Progress()
    {
        // Arrange - Test that retired member with 1 completed month shows 1/3 progress
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity in previous completed month (last month mid)
        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal = Rehearsal.Create(lastMonth, "Last Month Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should show 1/3 progress (1 completed month with activity)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue();
        result.ProgressMonths.Should().Be(1);
        result.ProgressTotalMonths.Should().Be(3);
        result.ProgressDescription.Should().Be("1/3 meses de atividade consecutiva");
    }

    [Fact]
    public async Task GetMemberStatusAsync_RetiredMemberWithCurrentMonthActivity_Shows1Of3Progress()
    {
        // Arrange - Test that retired member with only current month activity shows 1/3 progress
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity in current month only (yesterday)
        var yesterday = now.AddDays(-1);
        var rehearsal = Rehearsal.Create(yesterday, "Recent Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should show 1/3 progress (current month counts immediately)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue();
        result.ProgressMonths.Should().Be(1);
        result.ProgressTotalMonths.Should().Be(3);
    }

    [Fact]
    public async Task GetMemberStatusAsync_RetiredMemberWithGapInMonths_StopsCountingAtGap()
    {
        // Arrange - Test that gap in consecutive months stops the count
        // Example: Activity in current month and 2 months ago, but NOT in last month
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity in current month
        var yesterday = now.AddDays(-1);
        var rehearsal1 = Rehearsal.Create(yesterday, "Current Month");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // NO activity in last month (creates gap)

        // Add activity 2 months ago
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var rehearsal2 = Rehearsal.Create(twoMonthsAgo, "Two Months Ago");
        _context.Rehearsals.Add(rehearsal2);
        await _context.SaveChangesAsync();

        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attendance2.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance2);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should only count 1 month (current month) because last month has no activity
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue();
        result.ProgressMonths.Should().Be(1); // Only current month counts
        result.ProgressTotalMonths.Should().Be(3);
    }

    [Fact]
    public async Task GetMemberStatusAsync_SendsRetirementWarning_When1MonthLeft()
    {
        // Arrange - Test that warning is sent when active member has 1 month left
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = false;
        user.Nickname = "TestTuno";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Create existing status showing 2 months until reform (will change to 1)
        var existingStatus = new MemberStatus
        {
            UserId = userId,
            IsRetired = false,
            HasAnyActivity = true,
            ProgressMonths = 2,
            ProgressTotalMonths = 6,
            LastUpdatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        _context.MemberStatuses.Add(existingStatus);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // Add activity 5 months ago (so 4 consecutive COMPLETED months without activity)
        // With new logic: no current month activity adds 1 → 4 + 1 = 5 → ProgressMonths = 6 - 5 = 1
        // i=1: no activity (last month)
        // i=2: no activity
        // i=3: no activity
        // i=4: no activity
        // i=5: HAS activity → stops counting at 4 completed months without activity
        // Plus 1 for no current month activity = 5 total
        // 6 - 5 = 1 month until reform
        var fiveMonthsAgo = new DateTime(now.AddMonths(-5).Year, now.AddMonths(-5).Month, 15);
        var rehearsal = Rehearsal.Create(fiveMonthsAgo, "Old Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Create a service with push notifications enabled for this test
        var optionsWithNotifications = new Mock<IOptions<MemberStatusUpdateOptions>>();
        optionsWithNotifications.Setup(o => o.Value).Returns(new MemberStatusUpdateOptions
        {
            Enabled = true,
            ScheduledTime = "21:00",
            PushNotificationsEnabled = true
        });

        var serviceWithNotifications = new MemberStatusService(
            _context,
            _mockUserManager.Object,
            _mockPushNotificationService.Object,
            _mockAuditLogService.Object,
            _mockLogger.Object,
            optionsWithNotifications.Object);

        // Act
        var result = await serviceWithNotifications.UpdateMemberStatusAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse();
        result.ProgressMonths.Should().Be(1);

        // Verify warning notification was sent to the user
        _mockPushNotificationService.Verify(
            pns => pns.SendToUserAsync(userId, It.Is<RTUB.Application.DTOs.SendPushNotificationDto>(
                dto => dto.Title.Contains("1 mês até reforma"))),
            Times.Once);
    }

    [Fact]
    public async Task GetMemberStatusAsync_RetiredMemberWithDecJanActivity_AndFutureFebMarEnrollments_ShowsOnly2Of3()
    {
        // Arrange - CRITICAL BUG FIX TEST
        // Scenario: Member is retired, has activity in December and January (current month),
        // but is enrolled in future events in February and March.
        // Expected: Should show 2/3 progress (only Dec + Jan count), NOT 4/4 or active status
        // Future enrollments should NOT count toward reactivation
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Current month (January) - activity 5 days ago (PAST - should count)
        var currentMonthActivity = now.AddDays(-5);
        var rehearsal1 = Rehearsal.Create(currentMonthActivity, "January Rehearsal");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        // Last month (December) - mid-month (PAST - should count)
        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var event1 = Event.Create("December Event", lastMonth, "Location Dec", EventType.Atuacao);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;
        _context.Enrollments.Add(enrollment1);

        // NO activity in November (breaks consecutive chain at 2 months)

        // February enrollment (FUTURE - should NOT count)
        // AddMonths handles year transitions correctly (e.g., Jan + 1 = Feb)
        var february = new DateTime(now.AddMonths(1).Year, now.AddMonths(1).Month, 15);

        var event2 = Event.Create("February Event", february, "Location Feb", EventType.Atuacao);
        _context.Events.Add(event2);
        await _context.SaveChangesAsync();

        var enrollment2 = Enrollment.Create(userId, event2.Id);
        enrollment2.WillAttend = true;
        _context.Enrollments.Add(enrollment2);

        // March enrollment (FUTURE - should NOT count)
        var march = february.AddMonths(1);
        var event3 = Event.Create("March Event", march, "Location Mar", EventType.Atuacao);
        _context.Events.Add(event3);
        await _context.SaveChangesAsync();

        var enrollment3 = Enrollment.Create(userId, event3.Id);
        enrollment3.WillAttend = true;
        _context.Enrollments.Add(enrollment3);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should show 2/3 progress (only Dec + Jan count)
        // Future February and March enrollments should NOT count
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue(); // Should stay retired (only 2 consecutive months)
        result.ProgressMonths.Should().Be(2); // Only current month (Jan) + last month (Dec)
        result.ProgressTotalMonths.Should().Be(3);
        result.ProgressDescription.Should().Be("2/3 meses de atividade consecutiva");
    }

    [Fact]
    public async Task GetMemberStatusesBatchAsync_ConsistentWithIndividualQuery_ForRetiredMember()
    {
        // Arrange - Test that batch query returns same result as individual query
        // This tests the fix for the "Gestão de Membros Ativos" vs "DetailsModal" inconsistency
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity in current month and last month (2 consecutive months)
        var currentMonthActivity = now.AddDays(-2);
        var rehearsal1 = Rehearsal.Create(currentMonthActivity, "Current Month Rehearsal");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);

        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal2 = Rehearsal.Create(lastMonth, "Last Month Rehearsal");
        _context.Rehearsals.Add(rehearsal2);
        await _context.SaveChangesAsync();

        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attendance2.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance2);

        await _context.SaveChangesAsync();

        // First, get individual result (this updates the cache)
        var individualResult = await _service.GetMemberStatusAsync(userId);

        // Then, get batch result (this reads from cache and applies dynamic recalculation)
        var batchResult = await _service.GetMemberStatusesBatchAsync(new[] { userId });

        // Assert - Both should return the same values
        batchResult.Should().ContainKey(userId);
        var batchMemberResult = batchResult[userId];
        batchMemberResult.Should().NotBeNull();

        // Key assertion: IsRetired should be consistent
        batchMemberResult!.IsRetired.Should().Be(individualResult.IsRetired);

        // Progress should also be consistent
        batchMemberResult.ProgressMonths.Should().Be(individualResult.ProgressMonths);
        batchMemberResult.ProgressTotalMonths.Should().Be(individualResult.ProgressTotalMonths);
        batchMemberResult.ProgressDescription.Should().Be(individualResult.ProgressDescription);
        batchMemberResult.HasActivityInCurrentMonth.Should().Be(individualResult.HasActivityInCurrentMonth);
    }

    [Fact]
    public async Task GetMemberStatusAsync_RespectsOverrideRetiredFlag_WhenAdminManuallySetsActive()
    {
        // Arrange - Test that OverrideRetired flag prevents automatic status recalculation
        // Scenario: Admin manually sets member to active using "Tornar Ativo" button
        // Even if member doesn't have 3 consecutive months of activity, they should stay active
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = false; // Manually set to active by admin
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add activity only 1 month ago (not enough for natural reactivation)
        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal = Rehearsal.Create(lastMonth, "Last Month Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Create a cached status with OverrideRetired = true (simulating admin's manual activation)
        var cachedStatus = new MemberStatus
        {
            UserId = userId,
            IsRetired = false, // Manually set to active
            OverrideRetired = true, // This is the key flag
            HasAnyActivity = true,
            LastRehearsalDate = lastMonth,
            LastActivityDate = lastMonth,
            ProgressMonths = 5, // Would normally show 5 months until reform
            ProgressTotalMonths = 6,
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-30), // Fresh cache
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.MemberStatuses.Add(cachedStatus);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should stay active because OverrideRetired is true
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeFalse(); // Should stay active due to OverrideRetired
    }

    [Fact]
    public async Task GetMemberStatusAsync_UnapprovedRehearsalsDoNotCount()
    {
        // Arrange - CRITICAL BUG FIX TEST
        // Test that rehearsals with Attended = false (not yet approved) are NOT counted
        // This simulates when an admin creates a rehearsal but hasn't marked attendance yet
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired to test reactivation
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Create 3 past rehearsals but with Attended = false (unapproved)
        // If these counted, the user would become active (3/3)
        var rehearsalDates = new[]
        {
            now.AddDays(-5), // Current month - unapproved
            new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15), // Last month - unapproved
            new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15), // 2 months ago - unapproved
        };

        foreach (var date in rehearsalDates)
        {
            var rehearsal = Rehearsal.Create(date, $"Rehearsal on {date:yyyy-MM-dd}");
            _context.Rehearsals.Add(rehearsal);
            await _context.SaveChangesAsync();

            // Create attendance but DO NOT mark as attended (Attended = false by default)
            var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
            // attendance.Attended is false by default - simulating unapproved rehearsal
            _context.RehearsalAttendances.Add(attendance);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Unapproved rehearsals should NOT be counted
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeFalse(); // No approved activities
        result.IsRetired.Should().BeTrue(); // Should stay retired
    }

    [Fact]
    public async Task GetMemberStatusAsync_MixOfApprovedAndUnapprovedRehearsalsCountsOnlyApproved()
    {
        // Arrange - Test that only approved rehearsals count toward consecutive months
        // Scenario: User has activity in current month (approved), previous month (approved), 
        // and two months ago (unapproved). Should show 2/3, not 3/3.
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Start as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Current month - APPROVED rehearsal (1 day ago)
        var janRehearsal = Rehearsal.Create(now.AddDays(-1), "January Rehearsal");
        _context.Rehearsals.Add(janRehearsal);
        await _context.SaveChangesAsync();
        var janAttendance = RehearsalAttendance.Create(janRehearsal.Id, userId);
        janAttendance.MarkAttendance(true); // Approved
        _context.RehearsalAttendances.Add(janAttendance);

        // Last month (December) - APPROVED rehearsal
        var decDate = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var decRehearsal = Rehearsal.Create(decDate, "December Rehearsal");
        _context.Rehearsals.Add(decRehearsal);
        await _context.SaveChangesAsync();
        var decAttendance = RehearsalAttendance.Create(decRehearsal.Id, userId);
        decAttendance.MarkAttendance(true); // Approved
        _context.RehearsalAttendances.Add(decAttendance);

        // 2 months ago (November) - UNAPPROVED rehearsal (should NOT count)
        var novDate = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var novRehearsal = Rehearsal.Create(novDate, "November Rehearsal");
        _context.Rehearsals.Add(novRehearsal);
        await _context.SaveChangesAsync();
        var novAttendance = RehearsalAttendance.Create(novRehearsal.Id, userId);
        // NOT calling MarkAttendance - Attended = false (unapproved)
        _context.RehearsalAttendances.Add(novAttendance);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should only count approved months (Jan + Dec = 2)
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();
        result.IsRetired.Should().BeTrue(); // Should stay retired (only 2/3)
        result.ProgressMonths.Should().Be(2); // Only Jan + Dec
        result.ProgressTotalMonths.Should().Be(3);
        result.ProgressDescription.Should().Be("2/3 meses de atividade consecutiva");
    }

    [Fact]
    public async Task GetMemberStatusesBatchAsync_RespectsOverrideRetiredFlag()
    {
        // Arrange - Test that batch query also respects OverrideRetired flag
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = false; // Manually set to active by admin
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;

        // Add no recent activity (normally would be retired with 6+ months without activity)
        // Add activity 8 months ago (well past the 6-month threshold)
        var eightMonthsAgo = new DateTime(now.AddMonths(-8).Year, now.AddMonths(-8).Month, 15);
        var rehearsal = Rehearsal.Create(eightMonthsAgo, "Old Rehearsal");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Create a cached status with OverrideRetired = true (simulating admin's manual activation)
        var cachedStatus = new MemberStatus
        {
            UserId = userId,
            IsRetired = false, // Manually set to active by admin
            OverrideRetired = true, // This prevents automatic retirement
            HasAnyActivity = true,
            LastRehearsalDate = eightMonthsAgo,
            LastActivityDate = eightMonthsAgo,
            ProgressMonths = 0, // Would normally be "Próximo da reforma"
            ProgressTotalMonths = 6,
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-30), // Fresh cache
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.MemberStatuses.Add(cachedStatus);
        await _context.SaveChangesAsync();

        // Act
        var batchResult = await _service.GetMemberStatusesBatchAsync(new[] { userId });

        // Assert - Should stay active because OverrideRetired is true
        batchResult.Should().ContainKey(userId);
        var memberResult = batchResult[userId];
        memberResult.Should().NotBeNull();
        memberResult!.IsRetired.Should().BeFalse(); // Should stay active despite 8 months without activity
    }

    /// <summary>
    /// This test replicates the exact scenario reported by the user:
    /// - Member "Jeans" is retired
    /// - Activities in January 2026: 13th, 10th, 8th, 6th  
    /// - Activities in December 2025: 11th, 4th, 4th, 2nd, 2nd
    /// - NO activities in November 2025 or earlier
    /// - Today is January 14, 2026
    /// - Expected: 2/3 consecutive months (Jan + Dec)
    /// - Bug showed: 3/3 consecutive months (incorrect)
    /// </summary>
    [Fact]
    public async Task GetMemberStatusAsync_JeansScenario_ExactDatesJanDecOnly_Returns2Of3()
    {
        // Arrange - Replicate the exact scenario with fixed dates
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Jeans was retired
        user.Nickname = "Jeans";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Create January 2026 rehearsals (6th, 8th, 13th)
        var jan6 = new DateTime(2026, 1, 6);
        var jan8 = new DateTime(2026, 1, 8);
        var jan13 = new DateTime(2026, 1, 13);

        foreach (var date in new[] { jan6, jan8, jan13 })
        {
            var rehearsal = Rehearsal.Create(date, $"Rehearsal on {date:yyyy-MM-dd}");
            _context.Rehearsals.Add(rehearsal);
            await _context.SaveChangesAsync();

            var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
            attendance.MarkAttendance(true);
            _context.RehearsalAttendances.Add(attendance);
        }

        // Create January 2026 event (10th)
        var jan10Event = Event.Create("January Event", new DateTime(2026, 1, 10), "Location", EventType.Atuacao);
        _context.Events.Add(jan10Event);
        await _context.SaveChangesAsync();

        var jan10Enrollment = new Enrollment
        {
            EventId = jan10Event.Id,
            UserId = userId,
            WillAttend = true,
            EnrolledAt = DateTime.UtcNow
        };
        _context.Enrollments.Add(jan10Enrollment);

        // Create December 2025 rehearsals (2nd, 4th)
        var dec2 = new DateTime(2025, 12, 2);
        var dec4 = new DateTime(2025, 12, 4);

        foreach (var date in new[] { dec2, dec4 })
        {
            var rehearsal = Rehearsal.Create(date, $"Rehearsal on {date:yyyy-MM-dd}");
            _context.Rehearsals.Add(rehearsal);
            await _context.SaveChangesAsync();

            var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
            attendance.MarkAttendance(true);
            _context.RehearsalAttendances.Add(attendance);
        }

        // Create December 2025 events (2nd, 4th, 11th)
        foreach (var date in new[] { new DateTime(2025, 12, 2), new DateTime(2025, 12, 4), new DateTime(2025, 12, 11) })
        {
            var evt = Event.Create($"December Event {date:dd}", date, "Location", EventType.Atuacao);
            _context.Events.Add(evt);
            await _context.SaveChangesAsync();

            var enrollment = new Enrollment
            {
                EventId = evt.Id,
                UserId = userId,
                WillAttend = true,
                EnrolledAt = DateTime.UtcNow
            };
            _context.Enrollments.Add(enrollment);
        }

        await _context.SaveChangesAsync();

        // NO activities in November 2025 or earlier - intentionally empty

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.HasAnyActivity.Should().BeTrue();

        // The member should still be retired because 2 < 3 consecutive months required
        result.IsRetired.Should().BeTrue("member should still be retired with only 2 consecutive months");

        // Progress should show 2/3 (Jan + Dec only)
        result.ProgressMonths.Should().Be(2, "only January and December have activity, not November");
        result.ProgressTotalMonths.Should().Be(3);
        result.ProgressDescription.Should().Be("2/3 meses de atividade consecutiva");
    }

    private ApplicationUser CreateTestUser(string userId)
    {
        return new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            Email = $"user_{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestUser",
            PhoneNumber = "123456789",
            IsRetired = false
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
