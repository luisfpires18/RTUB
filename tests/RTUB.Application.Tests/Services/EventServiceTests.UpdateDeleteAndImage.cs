using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

// EventServiceTests: Update Delete And Image tests.
public partial class EventServiceTests
{
    [Fact]
    public async Task UpdateEventAsync_KeepsVisibility_UnlessItIsGiven()
    {
        var eventEntity = await _eventService.CreateEventAsync("Surpresa", DateTime.Now.AddDays(5), "Location", EventType.Festival, isPublic: false);

        await _eventService.UpdateEventAsync(eventEntity.Id, "Surpresa", DateTime.Now.AddDays(5), "Castelo", "", EventType.Festival);
        (await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id))!.IsPublic.Should().BeFalse("no visibility given keeps it private");

        await _eventService.UpdateEventAsync(eventEntity.Id, "Surpresa", DateTime.Now.AddDays(5), "Castelo", "", EventType.Festival, isPublic: true);
        (await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id))!.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateEventAsync_WithValidData_UpdatesEvent()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Original Event", DateTime.Now.AddDays(5), "Original Location", EventType.Festival, "Original Description");
        var newName = "Updated Event";
        var newDate = DateTime.Now.AddDays(10);
        var newLocation = "Updated Location";
        var newDescription = "Updated Description";
        var newType = EventType.Atuacao;

        // Act
        await _eventService.UpdateEventAsync(eventEntity.Id, newName, newDate, newLocation, newDescription, newType);
        var updated = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.Name.Should().Be(newName);
        updated.Date.Should().Be(newDate);
        updated.Location.Should().Be(newLocation);
        updated.Description.Should().Be(newDescription);
        updated.Type.Should().Be(newType);
    }

    [Fact]
    public async Task UpdateEventAsync_WithEnrollments_DoesNotThrowTrackingException()
    {
        // Arrange - This test reproduces the tracking issue when updating events with enrollments
        var user = new ApplicationUser
        {
            Id = "test-user-1",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestNick",
            PhoneNumber = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = await _eventService.CreateEventAsync("Event with Enrollments", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);

        var enrollment = Enrollment.Create(user.Id, eventEntity.Id);
        enrollment.WillAttend = true;
        enrollment.Instrument = InstrumentType.Guitarra;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Load the event with enrollments (simulating typical usage)
        var loadedEvent = await _context.Events
            .Include(e => e.Enrollments)
            .ThenInclude(en => en.User)
            .FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
        loadedEvent.Should().NotBeNull();

        // Act - Update the event (this should not throw tracking exception)
        var act = async () => await _eventService.UpdateEventAsync(eventEntity.Id, "Updated Name", DateTime.Now.AddDays(8), "Updated Location", "Updated Description", EventType.Atuacao);

        // Assert
        await act.Should().NotThrowAsync();

        var updated = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteEventAsync_WithExistingEvent_DeletesEvent()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Event to Delete", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var eventId = eventEntity.Id;

        // Act
        await _eventService.DeleteEventAsync(eventId);

        // Assert
        var deleted = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        deleted.Should().BeNull();
    }

    /// <summary>Task 074 (PR #275 review): an activity that accounted for the event stays, unlinked; others keep their link.</summary>
    [Fact]
    public async Task DeleteEventAsync_UnlinksItsActivities_AndKeepsThem()
    {
        var deleted = await _eventService.CreateEventAsync("Apagado", DateTime.Now.AddDays(-7), "Braga", EventType.Festival);
        var kept = await _eventService.CreateEventAsync("Mantido", DateTime.Now.AddDays(-6), "Braga", EventType.Festival);
        var linked = Activity.Create(1, "Serenata", DateTime.Today);
        linked.EventId = deleted.Id;
        var other = Activity.Create(1, "Outra", DateTime.Today);
        other.EventId = kept.Id;
        _context.Activities.AddRange(linked, other);
        await _context.SaveChangesAsync();

        await _eventService.DeleteEventAsync(deleted.Id);

        await using var db = _fixture.CreateContext();
        (await db.Activities.AsNoTracking().SingleAsync(a => a.Id == linked.Id)).EventId.Should().BeNull("the link goes with the event");
        (await db.Activities.AsNoTracking().SingleAsync(a => a.Id == other.Id)).EventId.Should().Be(kept.Id);
        (await db.Events.AnyAsync(e => e.Id == deleted.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteEventAsync_WithImage_DeletesImageFromStorage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Event with Image", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var imageUrl = "https://example.com/test-image.webp";
        eventEntity.SetImage(imageUrl);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id);

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(imageUrl),
            Times.Once,
            "Image should be deleted from storage when event is deleted");
    }

    [Fact]
    public async Task DeleteEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _eventService.DeleteEventAsync(9999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SetEventImageAsync_WithExistingEvent_SetsImage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var imageUrl = "https://example.com/new-image.webp";

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "test.webp", "image/webp");
        var updated = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);

        // Assert
        updated!.ImageUrl.Should().Be(imageUrl);
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "test.webp", "image/webp", "events", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SetEventImageAsync_WithExistingImage_DeletesOldImage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var oldImageUrl = "https://example.com/old-image.webp";
        var newImageUrl = "https://example.com/new-image.webp";
        eventEntity.SetImage(oldImageUrl);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "test.webp", "image/webp");

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once,
            "Old image should be deleted when setting new image");
    }

    [Fact]
    public async Task SetEventImageAsync_WithNonExistentEvent_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        var act = async () => await _eventService.SetEventImageAsync(9999, imageStream, "test.webp", "image/webp");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateEventAsync_WithEndDate_SetsEndDate()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(5), "Location", EventType.Festival);
        var newEndDate = DateTime.Now.AddDays(7);

        // Act
        await _eventService.UpdateEventAsync(eventEntity.Id, "Test Event", DateTime.Now.AddDays(5), "Location", "Description", EventType.Festival, newEndDate);
        var updated = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.EndDate.Should().Be(newEndDate);
    }

    [Fact]
    public async Task UpdateEventAsync_WithNullEndDate_ClearsEndDate()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(5), "Location", EventType.Festival, "Description", DateTime.Now.AddDays(7));
        var updatedEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
        updatedEvent!.EndDate.Should().NotBeNull(); // Verify it was set

        // Act
        await _eventService.UpdateEventAsync(eventEntity.Id, "Test Event", DateTime.Now.AddDays(5), "Location", "Description", EventType.Festival, null);
        var updated = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _eventService.UpdateEventAsync(9999, "Name", DateTime.Now, "Location", "Description", EventType.Festival);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }
}
