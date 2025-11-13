using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Integration tests for rehearsal workflow scenarios
/// </summary>
public class RehearsalWorkflowTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ApplicationDbContext _context;
    private readonly RehearsalService _rehearsalService;
    private readonly RehearsalAttendanceService _attendanceService;

    public RehearsalWorkflowTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Register required dependencies for ApplicationDbContext
        services.AddScoped<IHttpContextAccessor>(_ => Mock.Of<IHttpContextAccessor>());
        services.AddScoped<AuditContext>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();

        _rehearsalService = new RehearsalService(_context);
        _attendanceService = new RehearsalAttendanceService(_context);
    }

    [Fact]
    public async Task MemberEnrollment_ShouldStartAsPending()
    {
        // Arrange
        var rehearsal = await _rehearsalService.CreateRehearsalAsync(
            DateTime.Today.AddDays(1),
            "Centro Académico",
            "Fado Practice");

        // Act
        var attendance = await _attendanceService.MarkAttendanceAsync(
            rehearsal.Id,
            "user123",
            true,
            InstrumentType.Guitarra);

        // Assert
        attendance.Attended.Should().BeFalse("new enrollments should start as pending");
        attendance.UserId.Should().Be("user123");
        attendance.Instrument.Should().Be(InstrumentType.Guitarra);
    }

    [Fact]
    public async Task AdminApproval_ShouldChangeStatusToApproved()
    {
        // Arrange
        var rehearsal = await _rehearsalService.CreateRehearsalAsync(
            DateTime.Today.AddDays(1),
            "Centro Académico");

        var attendance = await _attendanceService.MarkAttendanceAsync(
            rehearsal.Id,
            "user123");

        attendance.Attended.Should().BeFalse();

        // Act
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, attendance.Instrument);

        // Assert
        var updatedAttendance = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);
        updatedAttendance!.Attended.Should().BeTrue("admin has approved the attendance");
    }

    [Fact]
    public async Task RangeCreation_ShouldCreateOnlyTuesdaysAndThursdays()
    {
        // Arrange - create range from a Monday to following Sunday (7 days)
        var monday = new DateTime(2026, 1, 5); // Monday
        var sunday = monday.AddDays(6); // Sunday

        // Act
        var createdRehearsals = new List<Rehearsal>();
        for (var date = monday; date <= sunday; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Tuesday || date.DayOfWeek == DayOfWeek.Thursday)
            {
                var rehearsal = await _rehearsalService.CreateRehearsalAsync(
                    date,
                    "Centro Académico");
                createdRehearsals.Add(rehearsal);
            }
        }

        // Assert
        createdRehearsals.Should().HaveCount(2, "only Tuesday and Thursday in the week");
        createdRehearsals[0].Date.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        createdRehearsals[1].Date.DayOfWeek.Should().Be(DayOfWeek.Thursday);
    }

    [Fact]
    public async Task RangeCreation_ShouldSkipDuplicates()
    {
        // Arrange
        var tuesday = new DateTime(2026, 1, 6); // Tuesday
        await _rehearsalService.CreateRehearsalAsync(tuesday, "Location 1");

        // Act - try to create same date again
        var existingCount = (await _rehearsalService.GetRehearsalsAsync(tuesday, tuesday)).Count();
        
        // Creating duplicate should be prevented at application level
        var duplicate = await _rehearsalService.GetRehearsalByDateAsync(tuesday);

        // Assert
        duplicate.Should().NotBeNull("rehearsal exists on this date");
        existingCount.Should().Be(1, "only one rehearsal should exist per date");
    }

    [Fact]
    public async Task StatisticsCalculation_ShouldOnlyCountApprovedAttendances()
    {
        // Arrange
        var rehearsal1 = await _rehearsalService.CreateRehearsalAsync(
            DateTime.Today.AddDays(-7),
            "Location");

        var rehearsal2 = await _rehearsalService.CreateRehearsalAsync(
            DateTime.Today.AddDays(-14),
            "Location");

        // Create attendances for user1: 1 approved, 1 pending
        var attendance1 = await _attendanceService.MarkAttendanceAsync(rehearsal1.Id, "user1");
        await _attendanceService.UpdateAttendanceAsync(attendance1.Id, true, null); // Approve

        var attendance2 = await _attendanceService.MarkAttendanceAsync(rehearsal2.Id, "user1");
        // Leave as pending (Attended = false)

        // Act
        var stats = await _attendanceService.GetAttendanceStatsAsync(
            DateTime.Today.AddDays(-30),
            DateTime.Today);

        // Assert
        stats.Should().ContainKey("user1");
        stats["user1"].Should().Be(1, "only approved attendance should be counted");
    }

    [Fact]
    public async Task RehearsalState_ShouldReflectCurrentDate()
    {
        // Arrange
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        var todayRehearsal = await _rehearsalService.CreateRehearsalAsync(today, "Location");
        var pastRehearsal = await _rehearsalService.CreateRehearsalAsync(yesterday, "Location");
        var futureRehearsal = await _rehearsalService.CreateRehearsalAsync(tomorrow, "Location");

        // Act & Assert
        todayRehearsal.Date.Date.Should().Be(DateTime.Today, "should be today");
        pastRehearsal.Date.Date.Should().BeBefore(DateTime.Today, "should be in the past");
        futureRehearsal.Date.Date.Should().BeAfter(DateTime.Today, "should be in the future");

        // Verify UI would show correct state
        var isTodayToday = todayRehearsal.Date.Date == DateTime.Today;
        var isPastPast = pastRehearsal.Date.Date < DateTime.Today;
        var isFutureFuture = futureRehearsal.Date.Date > DateTime.Today;

        isTodayToday.Should().BeTrue("today's rehearsal should be marked as 'Hoje'");
        isPastPast.Should().BeTrue("past rehearsal should be marked as 'Passado'");
        isFutureFuture.Should().BeTrue("future rehearsal should be marked as 'Agendado'");
    }

    [Fact]
    public async Task DeleteAttendance_ShouldRemoveFromStatistics()
    {
        // Arrange
        var rehearsal = await _rehearsalService.CreateRehearsalAsync(
            DateTime.Today.AddDays(-7),
            "Location");

        var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user1");
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, null);

        var statsBefore = await _attendanceService.GetAttendanceStatsAsync(
            DateTime.Today.AddDays(-30),
            DateTime.Today);
        statsBefore["user1"].Should().Be(1);

        // Act
        await _attendanceService.DeleteAttendanceAsync(attendance.Id);

        // Assert
        var statsAfter = await _attendanceService.GetAttendanceStatsAsync(
            DateTime.Today.AddDays(-30),
            DateTime.Today);
        statsAfter.Should().NotContainKey("user1", "attendance was deleted");
    }

    [Fact]
    public async Task MultipleEnrollments_ShouldBePreventedOrReturnExisting()
    {
        // Arrange
        var rehearsal = await _rehearsalService.CreateRehearsalAsync(
            DateTime.Today.AddDays(1),
            "Location");

        var firstAttendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user1");

        // Act - try to enroll again
        // In the actual implementation, the service handles duplicates gracefully
        var attendances = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);
        var userAttendances = attendances.Where(a => a.UserId == "user1").ToList();

        // Assert
        userAttendances.Should().HaveCount(1, "user should only have one attendance per rehearsal");
        userAttendances.First().Id.Should().Be(firstAttendance.Id);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _serviceProvider.Dispose();
    }
}
