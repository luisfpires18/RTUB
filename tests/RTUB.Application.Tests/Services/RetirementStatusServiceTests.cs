using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Services.Retirement;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RetirementStatusService
/// Tests retirement status evaluation based on activity history
/// </summary>
public class RetirementStatusServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly RetirementStatusService _retirementStatusService;
    private readonly UserProfileRepository _userProfileRepository;
    private readonly RehearsalAttendanceRepository _attendanceRepository;
    private readonly EnrollmentRepository _enrollmentRepository;

    public RetirementStatusServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _userProfileRepository = new UserProfileRepository(_fixture.CreateContextFactory());
        _attendanceRepository = new RehearsalAttendanceRepository(_fixture.CreateContextFactory());
        _enrollmentRepository = new EnrollmentRepository(_fixture.CreateContextFactory());

        _retirementStatusService = new RetirementStatusService(
            _attendanceRepository,
            _enrollmentRepository,
            _userProfileRepository);
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_UserWithNoHistory_ReturnsNotRetiredWithNoMinimumHistory()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse();
        result.HasMinimumHistory.Should().BeFalse();
        result.FirstActivityDate.Should().BeNull();
        result.LastActivityDate.Should().BeNull();
        result.MonthsSinceLastActivity.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_UserWithRecentActivity_ReturnsNotRetired()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddDays(-30));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddDays(-30));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse();
        result.HasMinimumHistory.Should().BeTrue();
        result.MonthsSinceLastActivity.Should().BeLessThan(6);
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_UserWithSixMonthGap_ReturnsRetired()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-7));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddMonths(-7));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeTrue();
        result.HasMinimumHistory.Should().BeTrue();
        result.MonthsSinceLastActivity.Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_RetiredUserWithThreeConsecutiveMonthsActivity_ReturnsNotRetired()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);

        // Old activity (7 months ago) - causes retirement
        var oldRehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-7));
        await CreateAttendedRehearsal(user.Id, oldRehearsal.Id, DateTime.UtcNow.AddMonths(-7));

        // Recent activity in 3 consecutive months - should return to active
        var now = DateTime.UtcNow;
        var rehearsal1 = await CreateTestRehearsal(now.AddMonths(-2));
        await CreateAttendedRehearsal(user.Id, rehearsal1.Id, now.AddMonths(-2));

        var rehearsal2 = await CreateTestRehearsal(now.AddMonths(-1));
        await CreateAttendedRehearsal(user.Id, rehearsal2.Id, now.AddMonths(-1));

        var rehearsal3 = await CreateTestRehearsal(now.AddDays(-5));
        await CreateAttendedRehearsal(user.Id, rehearsal3.Id, now.AddDays(-5));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse();
        result.HasMinimumHistory.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_CaloiroMember_IsEvaluated()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Caloiro);
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-7));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddMonths(-7));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeTrue(); // Should be retired after 6+ months
        result.HasMinimumHistory.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_LeitaoMember_IsNotEvaluated()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Leitao);
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-7));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddMonths(-7));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse(); // Leitao is not evaluated
        result.HasMinimumHistory.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_WithEventEnrollments_CountsAsActivity()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        var eventEntity = await CreateTestEvent(DateTime.UtcNow.AddDays(-30));
        await CreateConfirmedEnrollment(user.Id, eventEntity.Id, DateTime.UtcNow.AddDays(-30));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse();
        result.HasMinimumHistory.Should().BeTrue();
        result.MonthsSinceLastActivity.Should().BeLessThan(6);
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_MixedActivities_UsesAllActivities()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);

        // Rehearsal 7 months ago
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-7));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddMonths(-7));

        // Event enrollment 2 months ago
        var eventEntity = await CreateTestEvent(DateTime.UtcNow.AddMonths(-2));
        await CreateConfirmedEnrollment(user.Id, eventEntity.Id, DateTime.UtcNow.AddMonths(-2));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsRetired.Should().BeFalse(); // Recent event enrollment prevents retirement
        result.HasMinimumHistory.Should().BeTrue();
        result.MonthsSinceLastActivity.Should().BeLessThan(6);
    }

    [Fact]
    public async Task UpdateUserRetirementStatusAsync_StatusChanged_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        user.IsRetired = false;
        await _userProfileRepository.UpdateAsync(user);

        // Detach the user to avoid tracking issues
        _context.Entry(user).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        // Create old activity to trigger retirement
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-7));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddMonths(-7));

        // Act
        var updated = await _retirementStatusService.UpdateUserRetirementStatusAsync(user.Id);

        // Assert
        updated.Should().BeTrue();

        var updatedUser = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.IsRetired.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserRetirementStatusAsync_StatusUnchanged_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        user.IsRetired = false;
        await _userProfileRepository.UpdateAsync(user);

        // Create recent activity
        var rehearsal = await CreateTestRehearsal(DateTime.UtcNow.AddDays(-30));
        await CreateAttendedRehearsal(user.Id, rehearsal.Id, DateTime.UtcNow.AddDays(-30));

        // Act
        var updated = await _retirementStatusService.UpdateUserRetirementStatusAsync(user.Id);

        // Assert
        updated.Should().BeFalse();

        var updatedUser = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_OnlyWillAttendEnrollments_CountAsActivity()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        var event1 = await CreateTestEvent(DateTime.UtcNow.AddMonths(-2));
        var event2 = await CreateTestEvent(DateTime.UtcNow.AddMonths(-1));

        // Create one confirmed and one declined enrollment
        await CreateConfirmedEnrollment(user.Id, event1.Id, DateTime.UtcNow.AddMonths(-2));
        await CreateDeclinedEnrollment(user.Id, event2.Id, DateTime.UtcNow.AddMonths(-1));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.HasMinimumHistory.Should().BeTrue();
        // Only the confirmed enrollment should count
        result.LastActivityDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-2), TimeSpan.FromDays(2));
    }

    [Fact]
    public async Task EvaluateRetirementStatusAsync_OnlyAttendedRehearsals_CountAsActivity()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        var rehearsal1 = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-2));
        var rehearsal2 = await CreateTestRehearsal(DateTime.UtcNow.AddMonths(-1));

        // Create one attended and one not attended rehearsal
        await CreateAttendedRehearsal(user.Id, rehearsal1.Id, DateTime.UtcNow.AddMonths(-2));
        await CreateNotAttendedRehearsal(user.Id, rehearsal2.Id, DateTime.UtcNow.AddMonths(-1));

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.HasMinimumHistory.Should().BeTrue();
        // Only the attended rehearsal should count
        result.LastActivityDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-2), TimeSpan.FromDays(2));
    }

    /// <summary>
    /// This test replicates the exact bug scenario from the issue:
    /// - Member "Jeans" is retired
    /// - Has activity in January 2026 and December 2025 only (2 consecutive months)
    /// - Should NOT become active (needs 3 consecutive months)
    /// - The bug was that RetirementStatusService incorrectly allowed reactivation with < 3 months
    /// </summary>
    [Fact]
    public async Task EvaluateRetirementStatusAsync_RetiredMemberWithOnly2ConsecutiveMonths_StaysRetired()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        user.IsRetired = true; // Mark as retired
        await _userProfileRepository.UpdateAsync(user);

        var now = DateTime.UtcNow;

        // Create activities in current month (Jan 2026 scenario)
        var rehearsal1 = await CreateTestRehearsal(now.AddDays(-5));
        await CreateAttendedRehearsal(user.Id, rehearsal1.Id, now.AddDays(-5));

        // Create activities in previous month (Dec 2025 scenario)
        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal2 = await CreateTestRehearsal(lastMonth);
        await CreateAttendedRehearsal(user.Id, rehearsal2.Id, lastMonth);

        // NO activities in 2 months ago (Nov 2025 scenario) - this breaks the chain at 2 months

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert - Should stay retired (only 2 consecutive months, needs 3)
        result.Should().NotBeNull();
        result.HasMinimumHistory.Should().BeTrue();
        result.IsRetired.Should().BeTrue("member should stay retired with only 2 consecutive months of activity");
    }

    /// <summary>
    /// Companion test to verify that 3 consecutive months DOES trigger reactivation
    /// </summary>
    [Fact]
    public async Task EvaluateRetirementStatusAsync_RetiredMemberWith3ConsecutiveMonths_BecomesActive()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        user.IsRetired = true; // Mark as retired
        await _userProfileRepository.UpdateAsync(user);

        var now = DateTime.UtcNow;

        // Create activities in PAST months to ensure they count
        // We need 3 consecutive months including potentially the current month if we're past day 1

        // Most recent activity - as recent as possible but still in the past
        // Use midday to avoid any midnight boundary issues
        var mostRecentDate = now.Date.AddDays(-1).AddHours(12); // Yesterday at noon
        var rehearsal1 = await CreateTestRehearsal(mostRecentDate);
        await CreateAttendedRehearsal(user.Id, rehearsal1.Id, mostRecentDate);

        // If yesterday was in the previous month, we need to adjust our strategy
        // We need activities in 3 consecutive months
        DateTime month1, month2, month3;

        if (mostRecentDate.Month == now.Month)
        {
            // Yesterday was in the current month, so we have:
            // - Current month (most recent)
            // - Last month
            // - 2 months ago
            month1 = mostRecentDate;
            month2 = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
            month3 = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        }
        else
        {
            // Yesterday was in the previous month (e.g., Feb 1st and yesterday was Jan 31st)
            // So we have:
            // - Last month (yesterday)
            // - 2 months ago
            // - 3 months ago
            month1 = mostRecentDate;
            month2 = new DateTime(mostRecentDate.AddMonths(-1).Year, mostRecentDate.AddMonths(-1).Month, 15);
            month3 = new DateTime(mostRecentDate.AddMonths(-2).Year, mostRecentDate.AddMonths(-2).Month, 15);
        }

        // We already created rehearsal1 for month1, now create for month2 and month3
        var rehearsal2 = await CreateTestRehearsal(month2);
        await CreateAttendedRehearsal(user.Id, rehearsal2.Id, month2);

        var rehearsal3 = await CreateTestRehearsal(month3);
        await CreateAttendedRehearsal(user.Id, rehearsal3.Id, month3);

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert - Should become active (3 consecutive months)
        result.Should().NotBeNull();
        result.HasMinimumHistory.Should().BeTrue();
        result.IsRetired.Should().BeFalse("member should become active with 3 consecutive months of activity");
    }

    /// <summary>
    /// Test that a gap in consecutive months stops the count (even if there's activity earlier)
    /// </summary>
    [Fact]
    public async Task EvaluateRetirementStatusAsync_RetiredMemberWithGapInActivity_StaysRetired()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        user.IsRetired = true; // Mark as retired
        await _userProfileRepository.UpdateAsync(user);

        var now = DateTime.UtcNow;

        // Create activities in current month
        var rehearsal1 = await CreateTestRehearsal(now.AddDays(-5));
        await CreateAttendedRehearsal(user.Id, rehearsal1.Id, now.AddDays(-5));

        // NO activity in previous month - creates a gap

        // Create activities in 2 months ago
        var twoMonthsAgo = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 15);
        var rehearsal2 = await CreateTestRehearsal(twoMonthsAgo);
        await CreateAttendedRehearsal(user.Id, rehearsal2.Id, twoMonthsAgo);

        // Create activities in 3 months ago
        var threeMonthsAgo = new DateTime(now.AddMonths(-3).Year, now.AddMonths(-3).Month, 15);
        var rehearsal3 = await CreateTestRehearsal(threeMonthsAgo);
        await CreateAttendedRehearsal(user.Id, rehearsal3.Id, threeMonthsAgo);

        // Create activities in 4 months ago
        var fourMonthsAgo = new DateTime(now.AddMonths(-4).Year, now.AddMonths(-4).Month, 15);
        var rehearsal4 = await CreateTestRehearsal(fourMonthsAgo);
        await CreateAttendedRehearsal(user.Id, rehearsal4.Id, fourMonthsAgo);

        // Act
        var result = await _retirementStatusService.EvaluateRetirementStatusAsync(user.Id);

        // Assert - Should stay retired because consecutive count from current month is only 1
        // (even though months 2-4 have 3 consecutive months)
        result.Should().NotBeNull();
        result.HasMinimumHistory.Should().BeTrue();
        result.IsRetired.Should().BeTrue("member should stay retired because gap in activity breaks consecutive count");
    }

    /// <summary>
    /// Test UpdateUserRetirementStatusAsync to verify it doesn't change a retired user
    /// to active if they only have 2 consecutive months of activity
    /// </summary>
    [Fact]
    public async Task UpdateUserRetirementStatusAsync_RetiredMemberWithOnly2Months_StaysRetired()
    {
        // Arrange
        var user = await CreateTestUser(MemberCategory.Tuno);
        user.IsRetired = true; // Mark as retired
        await _userProfileRepository.UpdateAsync(user);

        // Detach the user entity to simulate a fresh database query in UpdateUserRetirementStatusAsync.
        // Without detaching, EF would return the same in-memory instance and the test wouldn't 
        // accurately verify that the service reads the IsRetired flag from the database.
        _context.Entry(user).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var now = DateTime.UtcNow;

        // Create activities in current month
        var rehearsal1 = await CreateTestRehearsal(now.AddDays(-5));
        await CreateAttendedRehearsal(user.Id, rehearsal1.Id, now.AddDays(-5));

        // Create activities in previous month
        var lastMonth = new DateTime(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 15);
        var rehearsal2 = await CreateTestRehearsal(lastMonth);
        await CreateAttendedRehearsal(user.Id, rehearsal2.Id, lastMonth);

        // NO activities in 2 months ago - only 2 consecutive months

        // Act
        var updated = await _retirementStatusService.UpdateUserRetirementStatusAsync(user.Id);

        // Assert - Should NOT have updated (user should stay retired)
        updated.Should().BeFalse("user should not be updated when only 2 consecutive months");

        var updatedUser = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.IsRetired.Should().BeTrue("user should remain retired");
    }

    // Helper methods
    private async Task<ApplicationUser> CreateTestUser(MemberCategory category)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"test{Guid.NewGuid()}@test.com",
            UserName = $"test{Guid.NewGuid()}",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestNick",
            PhoneNumber = "123456789",
            EmailConfirmed = true,
            Categories = new List<MemberCategory> { category }
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Rehearsal> CreateTestRehearsal(DateTime date)
    {
        var rehearsal = new Rehearsal
        {
            Date = date,
            Location = "Test Location",
            Theme = $"Test Theme {Guid.NewGuid()}",
            Notes = "Test Notes"
        };

        await _context.Rehearsals.AddAsync(rehearsal);
        await _context.SaveChangesAsync();
        return rehearsal;
    }

    private async Task<Event> CreateTestEvent(DateTime date)
    {
        var eventEntity = new Event
        {
            Name = $"Test Event {Guid.NewGuid()}",
            Date = date,
            Location = "Test Location",
            Type = EventType.Festival,
            Description = "Test Description"
        };

        await _context.Events.AddAsync(eventEntity);
        await _context.SaveChangesAsync();
        return eventEntity;
    }

    private async Task<RehearsalAttendance> CreateAttendedRehearsal(string userId, int rehearsalId, DateTime checkedInAt)
    {
        var attendance = RehearsalAttendance.Create(rehearsalId, userId);
        attendance.Attended = true;
        attendance.CheckedInAt = checkedInAt;

        await _context.RehearsalAttendances.AddAsync(attendance);
        await _context.SaveChangesAsync();
        return attendance;
    }

    private async Task<RehearsalAttendance> CreateNotAttendedRehearsal(string userId, int rehearsalId, DateTime checkedInAt)
    {
        var attendance = RehearsalAttendance.Create(rehearsalId, userId);
        attendance.Attended = false;
        attendance.CheckedInAt = checkedInAt;

        await _context.RehearsalAttendances.AddAsync(attendance);
        await _context.SaveChangesAsync();
        return attendance;
    }

    private async Task<Enrollment> CreateConfirmedEnrollment(string userId, int eventId, DateTime enrolledAt)
    {
        var enrollment = Enrollment.Create(userId, eventId);
        enrollment.WillAttend = true;
        enrollment.EnrolledAt = enrolledAt;

        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();
        return enrollment;
    }

    private async Task<Enrollment> CreateDeclinedEnrollment(string userId, int eventId, DateTime enrolledAt)
    {
        var enrollment = Enrollment.Create(userId, eventId);
        enrollment.WillAttend = false;
        enrollment.EnrolledAt = enrolledAt;

        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();
        return enrollment;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
