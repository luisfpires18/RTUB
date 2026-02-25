using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for EnrollmentRepository
/// Tests repository layer operations including batch operations
/// </summary>
public class EnrollmentRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly EnrollmentRepository _repository;

    public EnrollmentRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new EnrollmentRepository(_fixture.CreateContextFactory());
    }

    [Fact]
    public async Task DeleteByEventIdAsync_WithNoEnrollments_DoesNotThrow()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _repository.DeleteByEventIdAsync(eventEntity.Id);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByEventIdAsync_WithSingleEnrollment_DeletesEnrollment()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var enrollment = Enrollment.Create("user123", eventEntity.Id);
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByEventIdAsync(eventEntity.Id);

        // Assert
        var remainingEnrollments = await _context.Enrollments
            .Where(e => e.EventId == eventEntity.Id)
            .ToListAsync();
        remainingEnrollments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteByEventIdAsync_WithMultipleEnrollments_DeletesAllForEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create("user1", eventEntity.Id);
        var enrollment2 = Enrollment.Create("user2", eventEntity.Id);
        var enrollment3 = Enrollment.Create("user3", eventEntity.Id);
        _context.Enrollments.AddRange(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByEventIdAsync(eventEntity.Id);

        // Assert
        var remainingEnrollments = await _context.Enrollments
            .Where(e => e.EventId == eventEntity.Id)
            .ToListAsync();
        remainingEnrollments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteByEventIdAsync_DoesNotDeleteOtherEventEnrollments()
    {
        // Arrange
        var event1 = Event.Create("Event 1", DateTime.Now.AddDays(7), "Location 1", EventType.Festival);
        var event2 = Event.Create("Event 2", DateTime.Now.AddDays(8), "Location 2", EventType.Atuacao);
        _context.Events.AddRange(event1, event2);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create("user1", event1.Id);
        var enrollment2 = Enrollment.Create("user2", event1.Id);
        var enrollment3 = Enrollment.Create("user1", event2.Id);
        _context.Enrollments.AddRange(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByEventIdAsync(event1.Id);

        // Assert
        var event1Enrollments = await _context.Enrollments
            .Where(e => e.EventId == event1.Id)
            .ToListAsync();
        var event2Enrollments = await _context.Enrollments
            .Where(e => e.EventId == event2.Id)
            .ToListAsync();

        event1Enrollments.Should().BeEmpty();
        event2Enrollments.Should().HaveCount(1);
        event2Enrollments[0].UserId.Should().Be("user1");
    }

    [Fact]
    public async Task DeleteByEventIdAsync_WithNonExistentEventId_DoesNotThrow()
    {
        // Arrange - No enrollments exist
        var nonExistentEventId = 9999;

        // Act
        var act = async () => await _repository.DeleteByEventIdAsync(nonExistentEventId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByEventIdAsync_PreservesEnrollmentWithDifferentInstruments()
    {
        // Arrange
        var event1 = Event.Create("Event 1", DateTime.Now.AddDays(7), "Location 1", EventType.Festival);
        var event2 = Event.Create("Event 2", DateTime.Now.AddDays(8), "Location 2", EventType.Atuacao);
        _context.Events.AddRange(event1, event2);
        await _context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create("user1", event1.Id);
        enrollment1.Instrument = InstrumentType.Guitarra;
        enrollment1.Notes = "Notes 1";
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create("user1", event2.Id);
        enrollment2.Instrument = InstrumentType.Bandolim;
        enrollment2.Notes = "Notes 2";
        enrollment2.WillAttend = true;
        _context.Enrollments.AddRange(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByEventIdAsync(event1.Id);

        // Assert
        var remainingEnrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.EventId == event2.Id);
        remainingEnrollment.Should().NotBeNull();
        remainingEnrollment!.Instrument.Should().Be(InstrumentType.Bandolim);
        remainingEnrollment.Notes.Should().Be("Notes 2");
    }

    public void Dispose()
    {
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
