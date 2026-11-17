// MeetingIntegrationTests: Lifecycle tests.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Application;

public partial class MeetingIntegrationTests
{
    #region Meeting Lifecycle Tests

    [Fact]
    public async Task MeetingLifecycle_CreateEditDelete_WorksCorrectly()
    {
        // Arrange - Create a meeting
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Annual General Meeting",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Main Hall",
            Statement = "Discussion of annual budget and activities"
        };

        // Act - Create
        var created = await _meetingService.CreateMeetingAsync(meeting);

        // Assert - Created successfully
        created.Should().NotBeNull();
        created.Id.Should().BeGreaterThan(0);
        created.Title.Should().Be("Annual General Meeting");

        // Act - Edit
        created.Title = "Updated Annual General Meeting";
        created.Location = "Conference Room A";
        await _meetingService.UpdateMeetingAsync(created);

        // Assert - Updated successfully
        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var updated = await _context.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == created.Id);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Annual General Meeting");
        updated.Location.Should().Be("Conference Room A");

        // Act - Delete
        await _meetingService.DeleteMeetingAsync(created.Id);

        // Assert - Deleted successfully
        var deleted = await _context.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task MeetingCancellation_SetsCancellationPropertiesCorrectly()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "Emergency Meeting",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Urgent matters to discuss"
        };
        var created = await _meetingService.CreateMeetingAsync(meeting);

        // Act - Cancel the meeting
        created.IsCancelled = true;
        created.CancellationReason = "Insufficient quorum expected";
        await _meetingService.UpdateMeetingAsync(created);

        // Assert
        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var cancelled = await _context.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == created.Id);
        cancelled.Should().NotBeNull();
        cancelled!.IsCancelled.Should().BeTrue();
        cancelled.CancellationReason.Should().Be("Insufficient quorum expected");
    }

    [Fact]
    public async Task MeetingUncancellation_ClearsCancellationProperties()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "Veterans Council",
            Date = DateTime.UtcNow.AddDays(14),
            Statement = "Regular CV meeting",
            IsCancelled = true,
            CancellationReason = "Weather conditions"
        };
        var created = await _meetingService.CreateMeetingAsync(meeting);

        // Act - Uncancel the meeting
        created.IsCancelled = false;
        created.CancellationReason = null;
        await _meetingService.UpdateMeetingAsync(created);

        // Assert
        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var uncancelled = await _context.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == created.Id);
        uncancelled.Should().NotBeNull();
        uncancelled!.IsCancelled.Should().BeFalse();
        uncancelled.CancellationReason.Should().BeNull();
    }

    #endregion
}
