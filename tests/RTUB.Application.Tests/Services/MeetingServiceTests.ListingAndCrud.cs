// MeetingService: listing and crud.
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public partial class MeetingServiceTests
{
    [Fact]
    public async Task CreateMeetingAsync_WithValidData_ReturnsMeeting()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Test AGO Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Centro Académico",
            Statement = "This is a test meeting statement."
        };

        // Act
        var result = await _meetingService.CreateMeetingAsync(meeting);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Title.Should().Be("Test AGO Meeting");
        result.Type.Should().Be(MeetingType.AssembleiaGeralOrdinaria);
    }

    [Fact]
    public async Task GetAllMeetingsAsync_OrdersByDateCorrectly()
    {
        // Arrange
        var pastDate = DateTime.Now.AddDays(-7);
        var upcomingDate1 = DateTime.Now.AddDays(7);
        var upcomingDate2 = DateTime.Now.AddDays(14);

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Past Meeting",
            Date = pastDate,
            Statement = "Past statement"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Upcoming Meeting 2",
            Date = upcomingDate2,
            Statement = "Future statement 2"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Upcoming Meeting 1",
            Date = upcomingDate1,
            Statement = "Future statement 1"
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetAllMeetingsAsync(null, 1, 10, _veteranoUser.Id);

        // Assert
        var meetings = result.ToList();
        meetings.Should().HaveCount(3);
        // Upcoming meetings should come first, ordered by date
        meetings[0].Title.Should().Be("Upcoming Meeting 1");
        meetings[1].Title.Should().Be("Upcoming Meeting 2");
        // Past meetings come last
        meetings[2].Title.Should().Be("Past Meeting");
    }

    [Fact]
    public async Task GetAllMeetingsAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            _context.Meetings.Add(new Meeting
            {
                Type = MeetingType.AssembleiaGeralOrdinaria,
                Title = $"Meeting {i}",
                Date = DateTime.Now.AddDays(i),
                Statement = $"Statement {i}"
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var page1 = await _meetingService.GetAllMeetingsAsync(null, 1, 10, _veteranoUser.Id);
        var page2 = await _meetingService.GetAllMeetingsAsync(null, 2, 10, _veteranoUser.Id);

        // Assert
        page1.Should().HaveCount(10);
        page2.Should().HaveCount(5);
    }

    [Fact]
    public async Task UpdateMeetingAsync_WithValidData_UpdatesMeeting()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Original Title",
            Date = DateTime.Now.AddDays(7),
            Statement = "Original Statement"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        meeting.Title = "Updated Title";
        meeting.Statement = "Updated Statement";

        // Act
        await _meetingService.UpdateMeetingAsync(meeting);

        // Assert
        var updated = await _context.Meetings.FindAsync(meeting.Id);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Title");
        updated.Statement.Should().Be("Updated Statement");
    }

    [Fact]
    public async Task DeleteMeetingAsync_ExistingMeeting_DeletesMeeting()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Meeting to Delete",
            Date = DateTime.Now.AddDays(7),
            Statement = "Will be deleted"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        var meetingId = meeting.Id;

        // Act
        await _meetingService.DeleteMeetingAsync(meetingId);

        // Assert
        _context.ChangeTracker.Clear();
        var deleted = await _context.Meetings.FindAsync(meetingId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMeetingAsync_NonExistingMeeting_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _meetingService.DeleteMeetingAsync(999));
    }
}
