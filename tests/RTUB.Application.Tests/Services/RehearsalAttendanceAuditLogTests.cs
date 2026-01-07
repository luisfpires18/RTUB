using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Repositories;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Tests.Utilities;
using Xunit;
using FluentAssertions;

namespace RTUB.Application.Tests.Services;

public class RehearsalAttendanceAuditLogTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly ApplicationDbContext _context;
    private readonly RehearsalAttendanceService _attendanceService;
    private readonly Mock<IRetirementStatusService> _mockRetirementStatusService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public RehearsalAttendanceAuditLogTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _mockRetirementStatusService = new Mock<IRetirementStatusService>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockUserManager = MockHelpers.CreateMockUserManager();

        _attendanceService = new RehearsalAttendanceService(
            new RehearsalAttendanceRepository(_context),
            _mockRetirementStatusService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            _mockHttpContextAccessor.Object,
            _mockUserManager.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task CreateAttendanceWithApprovalAsync_ShouldCreateOnlyOneAuditLog()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestNick",
            PhoneNumber = "123456789"
        };
        await _context.Users.AddAsync(user);

        var rehearsal = new Rehearsal
        {
            Date = DateTime.UtcNow.AddDays(1),
            StartTime = TimeSpan.FromHours(19),
            EndTime = TimeSpan.FromHours(22),
            Location = "Test Location"
        };
        await _context.Rehearsals.AddAsync(rehearsal);
        await _context.SaveChangesAsync();

        // Clear audit logs to start fresh
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        await _attendanceService.CreateAttendanceWithApprovalAsync(
            rehearsal.Id, 
            user.Id, 
            InstrumentType.Cavaquinho, 
            null, 
            null
        );

        // Assert
        var auditLogs = await _context.AuditLogs
            .Where(a => a.EntityType == "RehearsalAttendance")
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        // Log all audit entries for debugging
        foreach (var log in auditLogs)
        {
            Console.WriteLine($"AuditLog: Action={log.Action}, Changes={log.Changes}, HasChanges={!string.IsNullOrEmpty(log.Changes)}");
        }

        auditLogs.Should().HaveCount(1, "only one audit log should be created");
        auditLogs[0].Action.Should().Be("Created", "the attendance was just created");
    }

    [Fact]
    public async Task CreateAttendanceWithApprovalAsync_ShouldNotCreateModifiedAuditLog()
    {
        // Arrange  
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser2",
            Email = "test2@example.com",
            FirstName = "Test2",
            LastName = "User2",
            Nickname = "TestNick2",
            PhoneNumber = "987654321"
        };
        await _context.Users.AddAsync(user);

        var rehearsal = new Rehearsal
        {
            Date = DateTime.UtcNow.AddDays(1),
            StartTime = TimeSpan.FromHours(19),
            EndTime = TimeSpan.FromHours(22),
            Location = "Test Location"
        };
        await _context.Rehearsals.AddAsync(rehearsal);
        await _context.SaveChangesAsync();

        // Clear audit logs to start fresh
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        await _attendanceService.CreateAttendanceWithApprovalAsync(
            rehearsal.Id, 
            user.Id, 
            InstrumentType.Guitarra, 
            "Test notes", 
            "Banjo"
        );

        // Assert - specifically check that no "Modified" audit log was created
        var modifiedLogs = await _context.AuditLogs
            .Where(a => a.EntityType == "RehearsalAttendance" && a.Action == "Modified")
            .ToListAsync();

        modifiedLogs.Should().BeEmpty("no modified audit log should be created when creating a new attendance");
    }
}
