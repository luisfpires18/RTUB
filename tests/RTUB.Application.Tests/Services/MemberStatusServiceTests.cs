using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberStatusService
/// Tests business logic for retirement status calculation and last activity tracking
/// </summary>
public class MemberStatusServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MemberStatusService _service;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

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

        _service = new MemberStatusService(_context, _mockUserManager.Object);
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
        // Arrange - Test that retired member returns to active after 3 consecutive COMPLETED months
        // NOTE: Current month is excluded from count - only COMPLETED months count
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Manually set as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Create activities in the last 3 COMPLETED months (not including current month)
        // Use mid-month dates to ensure they fall within the correct month boundaries
        var now = DateTime.UtcNow;
        
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
        
        // 3 months ago - mid-month
        var threeMonthsAgo = new DateTime(now.AddMonths(-3).Year, now.AddMonths(-3).Month, 15);
        var rehearsal3 = Rehearsal.Create(threeMonthsAgo, "Location 3");
        _context.Rehearsals.Add(rehearsal3);
        await _context.SaveChangesAsync();
        
        var attendance3 = RehearsalAttendance.Create(rehearsal3.Id, userId);
        attendance3.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance3);
        
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
        // Arrange - Test that retired member needs 3 consecutive COMPLETED months to become active
        // NOTE: Current month is excluded from count
        var userId = Guid.NewGuid().ToString();
        var user = CreateTestUser(userId);
        user.IsRetired = true; // Manually set as retired
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var now = DateTime.UtcNow;
        
        // 1 month ago - mid-month
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();
        
        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);
        
        // 2 months ago - mid-month
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var event1 = Event.Create("Event 1", twoMonthsAgo, "Location 1", EventType.Atuacao);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();
        
        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;
        _context.Enrollments.Add(enrollment1);
        
        // No activity 3 months ago - breaks the consecutive chain at 2 months
        
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
        
        // 1 month ago - mid-month
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();
        
        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);
        
        // 2 months ago - mid-month
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var event1 = Event.Create("Event 1", twoMonthsAgo, "Location 1", EventType.Atuacao);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();
        
        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;
        _context.Enrollments.Add(enrollment1);
        
        // Current month activity (should NOT count - current month is excluded)
        // Use a date that's definitely in the current month (mid-month)
        var currentMonthDate = new DateTime(now.Year, now.Month, 15);
        var rehearsal0 = Rehearsal.Create(currentMonthDate, "Location Current");
        _context.Rehearsals.Add(rehearsal0);
        await _context.SaveChangesAsync();
        
        var attendance0 = RehearsalAttendance.Create(rehearsal0.Id, userId);
        attendance0.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance0);
        
        // Future activity (should NOT count) 
        var futureDate = now.AddDays(10);
        var rehearsal2 = Rehearsal.Create(futureDate, "Location Future");
        _context.Rehearsals.Add(rehearsal2);
        await _context.SaveChangesAsync();
        
        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attendance2.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance2);
        
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMemberStatusAsync(userId);

        // Assert - Should count only 2 consecutive COMPLETED months (1 and 2 months ago), excluding current and future months
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
        
        // 1 month ago - mid-month regular rehearsal
        var oneMonthAgo = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal1 = Rehearsal.Create(oneMonthAgo, "Location 1");
        _context.Rehearsals.Add(rehearsal1);
        await _context.SaveChangesAsync();
        
        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attendance1.MarkAttendance(true);
        _context.RehearsalAttendances.Add(attendance1);
        
        // 2 months ago - mid-month regular event
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var event1 = Event.Create("Event 1", twoMonthsAgo, "Location 1", EventType.Atuacao);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();
        
        var enrollment1 = Enrollment.Create(userId, event1.Id);
        enrollment1.WillAttend = true;
        _context.Enrollments.Add(enrollment1);
        
        // 3 months ago - MULTI-DAY event that starts in month -3 but ends in month -2
        // This should be counted in the START month (3 months ago), not the END month (2 months ago)
        var threeMonthsAgo = new DateTime(now.AddMonths(-3).Year, now.AddMonths(-3).Month, 28); // Near end of month
        var twoMonthsAgoEnd = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 5); // Early in next month
        var event2 = Event.Create("Multi-day Event", threeMonthsAgo, "Location 2", EventType.Festival);
        event2.EndDate = twoMonthsAgoEnd; // Event spans two months
        _context.Events.Add(event2);
        await _context.SaveChangesAsync();
        
        var enrollment2 = Enrollment.Create(userId, event2.Id);
        enrollment2.WillAttend = true;
        _context.Enrollments.Add(enrollment2);
        
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
