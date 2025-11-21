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
            store.Object, null, null, null, null, null, null, null, null);
        
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
