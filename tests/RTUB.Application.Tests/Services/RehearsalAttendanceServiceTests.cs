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
    public async Task MarkAttendanceAsync_NewAttendance_SendsPushNotificationToOtherAttendees()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create existing attendances for other users
        var existingAttendance1 = RehearsalAttendance.Create(rehearsal.Id, "existingUser1", InstrumentType.Guitarra);
        var existingAttendance2 = RehearsalAttendance.Create(rehearsal.Id, "existingUser2", InstrumentType.Bandolim);
        _context.RehearsalAttendances.AddRange(existingAttendance1, existingAttendance2);
        await _context.SaveChangesAsync();

        var newUserId = "newUser";
        var newUser = new ApplicationUser { Id = newUserId, Nickname = "NewUser", FirstName = "Test" };
        
        // Setup mock factory to return a notification
        _mockPushNotificationFactory.Setup(x => x.CreateRehearsalAttendanceNotification(
            It.IsAny<Rehearsal>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto
            {
                Title = "Test Notification",
                Body = "Test Body",
                Icon = "/test.png",
                Url = "/test",
                Tag = "test-tag"
            });
        
        // Setup mock user manager
        _mockUserManager.Setup(x => x.FindByIdAsync(newUserId))
            .ReturnsAsync(newUser);

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, newUserId, true, InstrumentType.Guitarra);

        // Assert
        result.Should().NotBeNull();
        
        // Verify push notification service was called with correct recipient IDs (excluding the new user)
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(
                It.Is<List<string>>(recipients => 
                    recipients.Count == 2 && 
                    recipients.Contains("existingUser1") && 
                    recipients.Contains("existingUser2") && 
                    !recipients.Contains(newUserId)),
                It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkAttendanceAsync_UpdateAttendanceFromNotAttendingToAttending_SendsPushNotification()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create existing attendances for other users
        var existingAttendance = RehearsalAttendance.Create(rehearsal.Id, "existingUser", InstrumentType.Guitarra);
        _context.RehearsalAttendances.Add(existingAttendance);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var user = new ApplicationUser { Id = userId, Nickname = "TestUser", FirstName = "Test" };
        
        // Setup mock factory to return a notification
        _mockPushNotificationFactory.Setup(x => x.CreateRehearsalAttendanceNotification(
            It.IsAny<Rehearsal>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto
            {
                Title = "Test Notification",
                Body = "Test Body",
                Icon = "/test.png",
                Url = "/test",
                Tag = "test-tag"
            });
        
        // Setup mock user manager
        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Create initial attendance with WillAttend = false
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, false, null, "Can't make it");
        initial.WillAttend.Should().BeFalse();

        // Clear any mock calls from the initial creation
        _mockPushNotificationService.Invocations.Clear();

        // Act - Update to WillAttend = true
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Bandolim);

        // Assert
        updated.WillAttend.Should().BeTrue();
        
        // Verify push notification service was called
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(
                It.Is<List<string>>(recipients => 
                    recipients.Count == 1 && 
                    recipients.Contains("existingUser")),
                It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkAttendanceAsync_NoOtherAttendees_DoesNotSendNotification()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var user = new ApplicationUser { Id = userId, Nickname = "TestUser", FirstName = "Test" };
        
        // Setup mock user manager
        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act - First user to mark attendance (no other attendees)
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Guitarra);

        // Assert
        result.Should().NotBeNull();
        
        // Verify push notification service was NOT called (no other attendees)
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(
                It.IsAny<List<string>>(),
                It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);
    }

    [Fact]
    public async Task MarkAttendanceAsync_WillAttendFalse_DoesNotSendNotification()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create existing attendance for another user
        var existingAttendance = RehearsalAttendance.Create(rehearsal.Id, "existingUser", InstrumentType.Guitarra);
        _context.RehearsalAttendances.Add(existingAttendance);
        await _context.SaveChangesAsync();

        var userId = "user123";

        // Act - Mark attendance with WillAttend = false
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, false, null, "Can't make it");

        // Assert
        result.Should().NotBeNull();
        result.WillAttend.Should().BeFalse();
        
        // Verify push notification service was NOT called (user is not attending)
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(
                It.IsAny<List<string>>(),
                It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
