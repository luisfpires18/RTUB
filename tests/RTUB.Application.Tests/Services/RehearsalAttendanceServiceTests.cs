using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Tests.Utilities;
using RTUB.Application.Services;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RehearsalAttendanceService
/// Tests business logic and service layer operations
/// </summary>
public class RehearsalAttendanceServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly RehearsalAttendanceService _attendanceService;
    private readonly Mock<IRetirementStatusService> _mockRetirementStatusService;
    private readonly Mock<IMemberStatusService> _mockMemberStatusService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public RehearsalAttendanceServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockRetirementStatusService = new Mock<IRetirementStatusService>();
        _mockMemberStatusService = new Mock<IMemberStatusService>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockUserManager = MockHelpers.CreateMockUserManager();

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
    public async Task MarkAttendanceAsync_NewAttendance_CreatesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalId.Should().Be(rehearsal.Id);
        result.UserId.Should().Be(userId);
        result.Instrument.Should().Be(instrument);
        result.Attended.Should().BeFalse(); // Defaults to false (pending approval)
    }

    [Fact]
    public async Task MarkAttendanceAsync_ExistingAttendance_UpdatesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId, InstrumentType.Guitarra);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Bandolim);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(attendance.Id);
        result.Instrument.Should().Be(InstrumentType.Bandolim);
        result.Attended.Should().BeFalse(); // Stays pending until admin approval
    }

    [Fact]
    public async Task GetAttendanceByIdAsync_ExistingAttendance_ReturnsAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123");
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Clear change tracker to ensure fresh load
        _context.ChangeTracker.Clear();

        // Act
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(attendance.Id);
        result.UserId.Should().Be("user123");
    }

    [Fact]
    public async Task GetAttendanceByIdAsync_NonExistingAttendance_ReturnsNull()
    {
        // Act
        var result = await _attendanceService.GetAttendanceByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAttendancesByRehearsalIdAsync_ReturnsAllAttendances()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var att1 = RehearsalAttendance.Create(rehearsal.Id, "user1");
        var att2 = RehearsalAttendance.Create(rehearsal.Id, "user2");
        var att3 = RehearsalAttendance.Create(rehearsal.Id, "user3");
        _context.RehearsalAttendances.AddRange(att1, att2, att3);
        await _context.SaveChangesAsync();

        // Clear change tracker to ensure fresh load
        _context.ChangeTracker.Clear();

        // Act
        var result = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAttendancesByRehearsalIdAsync_IncludesRehearsalEntity()
    {
        // Arrange
        var rehearsalDate = DateTime.Now.AddDays(7);
        var rehearsal = Rehearsal.Create(rehearsalDate, "Test Location", "Test Theme");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user1");
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Clear change tracker to ensure fresh load
        _context.ChangeTracker.Clear();

        // Act
        var result = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);
        var firstAttendance = result.First();

        // Assert
        firstAttendance.Rehearsal.Should().NotBeNull();
        firstAttendance.Rehearsal!.Location.Should().Be("Test Location");
        firstAttendance.Rehearsal.Theme.Should().Be("Test Theme");
        firstAttendance.Rehearsal.Date.Date.Should().Be(rehearsalDate.Date);
    }

    [Fact]
    public async Task GetAttendancesByUserIdAsync_ReturnsUserAttendances()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(7), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(14), "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var userId = "user123";
        _context.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsal1.Id, userId));
        _context.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsal2.Id, userId));
        _context.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsal1.Id, "otherUser"));
        await _context.SaveChangesAsync();

        // Act
        var result = await _attendanceService.GetAttendancesByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.UserId == userId);
    }

    [Fact]
    public async Task UpdateAttendanceAsync_ValidId_UpdatesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123", InstrumentType.Guitarra);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, false, InstrumentType.Bandolim);

        // Assert
        var updated = await _context.RehearsalAttendances.FindAsync(attendance.Id);
        updated!.Attended.Should().BeFalse();
        updated.Instrument.Should().Be(InstrumentType.Bandolim);
    }

    [Fact]
    public async Task UpdateAttendanceAsync_NonExistingAttendance_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.UpdateAttendanceAsync(999, true, null);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteAttendanceAsync_ValidId_DeletesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123");
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        await _attendanceService.DeleteAttendanceAsync(attendance.Id);

        // Assert
        var deleted = await _context.RehearsalAttendances.FindAsync(attendance.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAttendanceAsync_NonExistingAttendance_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.DeleteAttendanceAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetUserAttendanceCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(-10), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(-5), "Location 2");
        var rehearsal3 = Rehearsal.Create(DateTime.Now.AddDays(-2), "Location 3");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2, rehearsal3);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var attended1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attended1.MarkAttendance(true); // Approve
        var attended2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attended2.MarkAttendance(true); // Approve
        var notAttended = RehearsalAttendance.Create(rehearsal3.Id, userId);
        notAttended.MarkAttendance(false); // Not approved

        _context.RehearsalAttendances.AddRange(attended1, attended2, notAttended);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now.AddDays(-15);
        var endDate = DateTime.Now;

        // Act
        var result = await _attendanceService.GetUserAttendanceCountAsync(userId, startDate, endDate);

        // Assert
        result.Should().Be(2); // Only attended/approved rehearsals count
    }

    [Fact]
    public async Task GetAttendanceStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(-10), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(-5), "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var att1 = RehearsalAttendance.Create(rehearsal1.Id, "user1");
        att1.MarkAttendance(true); // Approve
        var att2 = RehearsalAttendance.Create(rehearsal2.Id, "user1");
        att2.MarkAttendance(true); // Approve
        var att3 = RehearsalAttendance.Create(rehearsal1.Id, "user2");
        att3.MarkAttendance(true); // Approve
        _context.RehearsalAttendances.AddRange(att1, att2, att3);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now.AddDays(-15);
        var endDate = DateTime.Now;

        // Act
        var result = await _attendanceService.GetAttendanceStatsAsync(startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
        result["user1"].Should().Be(2);
        result["user2"].Should().Be(1);
    }

    [Fact]
    public async Task GetAttendanceStatsAsync_ExcludesNotAttended()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-5), "Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attended = RehearsalAttendance.Create(rehearsal.Id, "user1");
        attended.MarkAttendance(true); // Approve
        var notAttended = RehearsalAttendance.Create(rehearsal.Id, "user2");
        notAttended.MarkAttendance(false); // Not approved

        _context.RehearsalAttendances.AddRange(attended, notAttended);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now.AddDays(-10);
        var endDate = DateTime.Now;

        // Act
        var result = await _attendanceService.GetAttendanceStatsAsync(startDate, endDate);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("user1");
        result.Should().NotContainKey("user2");
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithNotes_PersistsNotes()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var notes = "These are my notes for the rehearsal";

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, notes);

        // Assert
        result.Should().NotBeNull();
        result.Notes.Should().Be(notes);

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == result.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().Be(notes);
    }

    [Fact]
    public async Task MarkAttendanceAsync_UpdateExistingNotes_UpdatesCorrectly()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var initialNotes = "Initial notes";
        var updatedNotes = "Updated notes after change";

        // Create initial attendance with notes
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, initialNotes);
        initial.Notes.Should().Be(initialNotes);

        // Act - Update with new notes
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, updatedNotes);

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Notes.Should().Be(updatedNotes);

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().Be(updatedNotes);
    }

    [Fact]
    public async Task MarkAttendanceAsync_ClearNotes_AllowsClearingNotes()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var initialNotes = "Initial notes";

        // Create initial attendance with notes
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, initialNotes);
        initial.Notes.Should().Be(initialNotes);

        // Act - Clear notes by passing null
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, null);

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Notes.Should().BeNull();

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().BeNull();
    }

    [Fact]
    public async Task MarkAttendanceAsync_ClearNotesWithEmptyString_AllowsClearingNotes()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var initialNotes = "Initial notes";

        // Create initial attendance with notes
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, initialNotes);
        initial.Notes.Should().Be(initialNotes);

        // Act - Clear notes by passing empty string
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, "");

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Notes.Should().Be("");

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().Be("");
    }

    [Fact]
    public async Task MarkAttendanceAsync_ExistingAttendanceWithInstrument_ClearsInstrumentWhenNullPassed()
    {
        // Arrange - Create attendance with instrument
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var initialInstrument = InstrumentType.Guitarra;

        // Create attendance with instrument
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, initialInstrument);
        initial.Instrument.Should().Be(initialInstrument);

        // Act - Clear instrument by passing null (simulates user turning off "Quero tocar" toggle)
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Instrument.Should().BeNull(); // Instrument should be cleared

        // Verify in database
        _context.ChangeTracker.Clear();
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Instrument.Should().BeNull();
    }

    [Fact]
    public async Task MarkAttendanceAsync_ToggleOffThenOn_CanClearAndRestoreInstrument()
    {
        // Arrange - Create attendance with instrument
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var initialInstrument = InstrumentType.Guitarra;
        var newInstrument = InstrumentType.Bandolim;

        // Create attendance with instrument (toggle ON)
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, initialInstrument);
        initial.Instrument.Should().Be(initialInstrument);

        // Act 1 - Turn toggle OFF (clear instrument)
        var clearedResult = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);
        clearedResult.Instrument.Should().BeNull();

        // Verify cleared in database
        _context.ChangeTracker.Clear();
        var clearedFromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        clearedFromDb!.Instrument.Should().BeNull();

        // Act 2 - Turn toggle back ON (set new instrument)
        var restoredResult = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, newInstrument);

        // Assert
        restoredResult.Id.Should().Be(initial.Id); // Same attendance record
        restoredResult.Instrument.Should().Be(newInstrument);

        // Verify in database
        _context.ChangeTracker.Clear();
        var restoredFromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        restoredFromDb.Should().NotBeNull();
        restoredFromDb!.Instrument.Should().Be(newInstrument);
    }

    [Fact]
    public async Task MarkAttendanceAsync_WillAttendFalseWithNullInstrument_ClearsInstrumentCorrectly()
    {
        // Arrange - Create attendance with instrument (user was going and playing)
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var initialInstrument = InstrumentType.Guitarra;

        // Create attendance - user is going and wants to play
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, initialInstrument);
        initial.WillAttend.Should().BeTrue();
        initial.Instrument.Should().Be(initialInstrument);

        // Act - User changes to NOT going (and instrument should be cleared)
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, false, null, "Can't make it today");

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.WillAttend.Should().BeFalse();
        updated.Instrument.Should().BeNull(); // Instrument should be cleared when not attending
        updated.Notes.Should().Be("Can't make it today");

        // Verify in database
        _context.ChangeTracker.Clear();
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.WillAttend.Should().BeFalse();
        fromDb.Instrument.Should().BeNull();
        fromDb.Notes.Should().Be("Can't make it today");
    }

    [Fact]
    public async Task MarkAttendanceAsync_NonAttendance_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser 
        { 
            Id = "user1", 
            UserName = "user1", 
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One" 
        };
        var attendingUser2 = new ApplicationUser 
        { 
            Id = "user2", 
            UserName = "user2", 
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two" 
        };
        var notAttendingUser = new ApplicationUser 
        { 
            Id = "user3", 
            UserName = "user3", 
            Email = "user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three" 
        };
        var newNonAttendingUser = new ApplicationUser 
        { 
            Id = "user4", 
            UserName = "user4", 
            Email = "user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four" 
        };
        
        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, newNonAttendingUser);

        // Create existing attendances
        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1", InstrumentType.Guitarra);
        attendance1.WillAttend = true;
        
        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2", InstrumentType.Baixo);
        attendance2.WillAttend = true;
        
        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3", InstrumentType.Bandolim);
        attendance3.WillAttend = false; // This user is NOT attending
        
        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        _mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) => 
            {
                capturedRecipients.AddRange(recipients);
            })
            .Returns(Task.CompletedTask);

        _mockPushNotificationFactory
            .Setup(x => x.CreateRehearsalNonAttendanceNotification(It.IsAny<Rehearsal>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        // Act - New user marks non-attendance
        await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user4", willAttend: false);

        // Assert - Verify notification was sent only to users with WillAttend = true
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("user1", "user1 is attending");
        capturedRecipients.Should().Contain("user2", "user2 is attending");
        capturedRecipients.Should().NotContain("user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("user4", "user4 is the one marking non-attendance");
    }

    [Fact]
    public async Task MarkAttendanceAsync_Attendance_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser 
        { 
            Id = "user1", 
            UserName = "user1", 
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One" 
        };
        var attendingUser2 = new ApplicationUser 
        { 
            Id = "user2", 
            UserName = "user2", 
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two" 
        };
        var notAttendingUser = new ApplicationUser 
        { 
            Id = "user3", 
            UserName = "user3", 
            Email = "user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three" 
        };
        var newAttendingUser = new ApplicationUser 
        { 
            Id = "user4", 
            UserName = "user4", 
            Email = "user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four" 
        };
        
        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, newAttendingUser);

        // Create existing attendances
        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1", InstrumentType.Guitarra);
        attendance1.WillAttend = true;
        
        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2", InstrumentType.Baixo);
        attendance2.WillAttend = true;
        
        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3", InstrumentType.Bandolim);
        attendance3.WillAttend = false; // This user is NOT attending
        
        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        _mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) => 
            {
                capturedRecipients.AddRange(recipients);
            })
            .Returns(Task.CompletedTask);

        _mockPushNotificationFactory
            .Setup(x => x.CreateRehearsalAttendanceNotification(It.IsAny<Rehearsal>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        // Act - New user marks attendance
        await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user4", willAttend: true);

        // Assert - Verify notification was sent only to users with WillAttend = true
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("user1", "user1 is attending");
        capturedRecipients.Should().Contain("user2", "user2 is attending");
        capturedRecipients.Should().NotContain("user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("user4", "user4 is the one marking attendance");
    }

    [Fact]
    public async Task MarkAttendanceAsync_ChangingToNonAttendance_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser 
        { 
            Id = "user1", 
            UserName = "user1", 
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One" 
        };
        var attendingUser2 = new ApplicationUser 
        { 
            Id = "user2", 
            UserName = "user2", 
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two" 
        };
        var notAttendingUser = new ApplicationUser 
        { 
            Id = "user3", 
            UserName = "user3", 
            Email = "user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three" 
        };
        var changingUser = new ApplicationUser 
        { 
            Id = "user4", 
            UserName = "user4", 
            Email = "user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four" 
        };
        
        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, changingUser);

        // Create existing attendances
        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1", InstrumentType.Guitarra);
        attendance1.WillAttend = true;
        
        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2", InstrumentType.Baixo);
        attendance2.WillAttend = true;
        
        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3", InstrumentType.Bandolim);
        attendance3.WillAttend = false; // This user is NOT attending
        
        var attendance4 = RehearsalAttendance.Create(rehearsal.Id, "user4", InstrumentType.Cavaquinho);
        attendance4.WillAttend = true; // This user is currently attending but will change
        
        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3, attendance4);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        _mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) => 
            {
                capturedRecipients.AddRange(recipients);
            })
            .Returns(Task.CompletedTask);

        _mockPushNotificationFactory
            .Setup(x => x.CreateRehearsalCancellationNotification(It.IsAny<Rehearsal>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        // Act - User changes from attending to not attending
        await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user4", willAttend: false);

        // Assert - Verify notification was sent only to users with WillAttend = true (excluding the changing user)
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("user1", "user1 is attending");
        capturedRecipients.Should().Contain("user2", "user2 is attending");
        capturedRecipients.Should().NotContain("user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("user4", "user4 is the one cancelling attendance");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
