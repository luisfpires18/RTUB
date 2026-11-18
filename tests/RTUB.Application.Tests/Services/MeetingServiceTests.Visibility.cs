// MeetingService: visibility.
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MeetingServiceTests
{
    [Fact]
    public async Task GetAllMeetingsAsync_NonVeteranoUser_FiltersOutCVMeetings()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "AGO Statement"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.Now.AddDays(8),
            Statement = "CV Statement"
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetAllMeetingsAsync(null, 1, 10, _nonVeteranoUser.Id);

        // Assert
        var meetings = result.ToList();
        meetings.Should().HaveCount(1);
        meetings[0].Type.Should().Be(MeetingType.AssembleiaGeralOrdinaria);
        meetings.Should().NotContain(m => m.Type == MeetingType.ConselhoVeteranos);
    }

    [Fact]
    public async Task GetAllMeetingsAsync_VeteranoUser_IncludesCVMeetings()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "AGO Statement"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.Now.AddDays(8),
            Statement = "CV Statement"
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetAllMeetingsAsync(null, 1, 10, _veteranoUser.Id);

        // Assert
        var meetings = result.ToList();
        meetings.Should().HaveCount(2);
        meetings.Should().Contain(m => m.Type == MeetingType.ConselhoVeteranos);
    }

    [Fact]
    public async Task GetAllMeetingsAsync_TunossauroUser_IncludesCVMeetings()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "AGO Statement"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.Now.AddDays(8),
            Statement = "CV Statement"
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetAllMeetingsAsync(null, 1, 10, _tunossauroUser.Id);

        // Assert
        var meetings = result.ToList();
        meetings.Should().HaveCount(2);
        meetings.Should().Contain(m => m.Type == MeetingType.ConselhoVeteranos);
    }

    [Fact]
    public async Task GetAllMeetingsAsync_WithSearchTerm_FiltersCorrectly()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Budget Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "Discussing annual budget"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "Emergency Meeting",
            Date = DateTime.Now.AddDays(8),
            Statement = "Urgent matters to discuss"
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetAllMeetingsAsync("budget", 1, 10, _veteranoUser.Id);

        // Assert
        var meetings = result.ToList();
        meetings.Should().HaveCount(1);
        meetings[0].Title.Should().Contain("Budget");
    }

    [Fact]
    public async Task GetAllMeetingsAsync_LeitaoUser_FiltersOutAssembleiaGeralMeetings()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "AGO Statement"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "AGE Meeting",
            Date = DateTime.Now.AddDays(8),
            Statement = "AGE Statement"
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetAllMeetingsAsync(null, 1, 10, _leitaoUser.Id);

        // Assert
        var meetings = result.ToList();
        meetings.Should().HaveCount(0); // Leitão can't see AG meetings
        meetings.Should().NotContain(m => m.Type == MeetingType.AssembleiaGeralOrdinaria);
        meetings.Should().NotContain(m => m.Type == MeetingType.AssembleiaGeralExtraordinaria);
    }
}
