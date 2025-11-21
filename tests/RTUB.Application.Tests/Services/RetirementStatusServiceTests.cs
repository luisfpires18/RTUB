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
        _userProfileRepository = new UserProfileRepository(_context);
        _attendanceRepository = new RehearsalAttendanceRepository(_context);
        _enrollmentRepository = new EnrollmentRepository(_context);
        
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
