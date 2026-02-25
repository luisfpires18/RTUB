using FluentAssertions;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingParticipationService enlist/participation time behavior.
/// </summary>
public class MeetingParticipationServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MeetingParticipationService _service;

    public MeetingParticipationServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _service = new MeetingParticipationService(new MeetingParticipationRepository(_fixture.CreateContextFactory()));
    }

    [Fact]
    public async Task UpdateParticipationAsync_WhenWillAttendDoesNotChange_DoesNotUpdateParticipatedAt()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(3),
            Location = "Location",
            Statement = "Test Statement",
            Type = RTUB.Core.Enums.MeetingType.AssembleiaGeralOrdinaria
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var participation = await _service.CreateParticipationAsync("user1", meeting.Id, willAttend: true);
        var originalParticipatedAt = participation.ParticipatedAt;

        // Act - update without changing WillAttend
        var updated = await _service.UpdateParticipationAsync(participation.Id, willAttend: true, notes: "Updated notes");

        // Assert
        updated.Id.Should().Be(participation.Id);
        updated.ParticipatedAt.Should().Be(originalParticipatedAt, "participation time should not change when WillAttend does not change");
    }

    [Fact]
    public async Task UpdateParticipationAsync_WhenWillAttendChanges_UpdatesParticipatedAt()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(3),
            Location = "Location",
            Statement = "Test Statement",
            Type = RTUB.Core.Enums.MeetingType.AssembleiaGeralOrdinaria
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var participation = await _service.CreateParticipationAsync("user1", meeting.Id, willAttend: true);
        var originalParticipatedAt = participation.ParticipatedAt;

        // Act - toggle attendance
        var updated = await _service.UpdateParticipationAsync(participation.Id, willAttend: false, notes: "Not going");

        // Assert
        updated.Id.Should().Be(participation.Id);
        updated.WillAttend.Should().BeFalse();
        updated.ParticipatedAt.Should().BeAfter(originalParticipatedAt, "participation time should be refreshed when WillAttend changes");
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}

