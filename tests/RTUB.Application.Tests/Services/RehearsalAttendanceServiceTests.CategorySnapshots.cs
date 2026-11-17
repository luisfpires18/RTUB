// RehearsalAttendanceService: category snapshots.
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RehearsalAttendanceServiceTests
{
    [Fact]
    public async Task MarkAttendanceAsync_SnapshotsUserPrimaryCategoryAtRehearsal()
    {
        // Arrange — seed Leitao user; new attendance must capture Leitao snapshot
        var user = new ApplicationUser
        {
            Id = "snap-att-1",
            FirstName = "S",
            LastName = "A",
            Nickname = "SA",
            Email = "sa@example.com",
            UserName = "sa@example.com",
            Categories = new List<MemberCategory> { MemberCategory.Leitao }
        };
        _context.Users.Add(user);
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Loc");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var mockUserManager = MockHelpers.CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var service = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            mockUserManager.Object,
            Mock.Of<ILogger<RehearsalAttendanceService>>());

        // Act
        var attendance = await service.MarkAttendanceAsync(
            rehearsal.Id, user.Id, willAttend: true, instrument: null, notes: null, otherInstruments: null, skipNotification: true);

        // Assert
        attendance.CategoryAtRehearsal.Should().Be(MemberCategory.Leitao);
    }

    [Fact]
    public async Task CreateAttendanceWithApprovalAsync_SnapshotsUserPrimaryCategoryAtRehearsal()
    {
        // Arrange — admin-add path with Tuno user
        var user = new ApplicationUser
        {
            Id = "snap-att-2",
            FirstName = "S",
            LastName = "B",
            Nickname = "SB",
            Email = "sb@example.com",
            UserName = "sb@example.com",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };
        _context.Users.Add(user);
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(8), "Loc");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var mockUserManager = MockHelpers.CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var service = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            mockUserManager.Object,
            Mock.Of<ILogger<RehearsalAttendanceService>>());

        // Act
        var attendance = await service.CreateAttendanceWithApprovalAsync(rehearsal.Id, user.Id);

        // Assert
        attendance.CategoryAtRehearsal.Should().Be(MemberCategory.Tuno);
        attendance.Attended.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAttendanceAsync_UserNotFound_SnapshotsNull()
    {
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(9), "Loc");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var mockUserManager = MockHelpers.CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var service = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            mockUserManager.Object,
            Mock.Of<ILogger<RehearsalAttendanceService>>());

        var attendance = await service.MarkAttendanceAsync(
            rehearsal.Id, "nobody", willAttend: true, instrument: null, notes: null, otherInstruments: null, skipNotification: true);

        attendance.CategoryAtRehearsal.Should().BeNull();
    }
}
