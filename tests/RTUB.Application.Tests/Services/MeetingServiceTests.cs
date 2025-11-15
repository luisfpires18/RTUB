using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingService
/// Tests business logic for meeting operations and Veterano visibility filtering
/// </summary>
public class MeetingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MeetingService _meetingService;
    private readonly ApplicationUser _veteranoUser;
    private readonly ApplicationUser _nonVeteranoUser;

    public MeetingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _meetingService = new MeetingService(_context);

        // Disable auditing for test setup
        _context.DisableAuditing();

        // Create test users
        _veteranoUser = new ApplicationUser
        {
            Id = "veterano-user-id",
            UserName = "veterano@test.com",
            NormalizedUserName = "VETERANO@TEST.COM",
            Email = "veterano@test.com",
            NormalizedEmail = "VETERANO@TEST.COM",
            FirstName = "Veterano",
            LastName = "User",
            Nickname = "VetTest",
            PhoneContact = "123456789",
            CategoriesJson = "[1]"  // Veterano enum value
        };

        _nonVeteranoUser = new ApplicationUser
        {
            Id = "non-veterano-user-id",
            UserName = "regular@test.com",
            NormalizedUserName = "REGULAR@TEST.COM",
            Email = "regular@test.com",
            NormalizedEmail = "REGULAR@TEST.COM",
            FirstName = "Regular",
            LastName = "User",
            Nickname = "RegTest",
            PhoneContact = "987654321",
            CategoriesJson = "[0]"  // Tuno enum value
        };

        _context.Users.Add(_veteranoUser);
        _context.Users.Add(_nonVeteranoUser);
        _context.SaveChanges();
        
        // Re-enable auditing
        _context.EnableAuditing();
    }

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
    public async Task GetMeetingByIdAsync_CVMeeting_NonVeteranoUser_ReturnsNull()
    {
        // Arrange
        var cvMeeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "CV Statement"
        };
        _context.Meetings.Add(cvMeeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetMeetingByIdAsync(cvMeeting.Id, _nonVeteranoUser.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMeetingByIdAsync_CVMeeting_VeteranoUser_ReturnsMeeting()
    {
        // Arrange
        var cvMeeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "CV Statement"
        };
        _context.Meetings.Add(cvMeeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetMeetingByIdAsync(cvMeeting.Id, _veteranoUser.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cvMeeting.Id);
        result.Type.Should().Be(MeetingType.ConselhoVeteranos);
    }

    [Fact]
    public async Task GetMeetingByIdAsync_AGOMeeting_AnyUser_ReturnsMeeting()
    {
        // Arrange
        var agoMeeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "AGO Statement"
        };
        _context.Meetings.Add(agoMeeting);
        await _context.SaveChangesAsync();

        // Act
        var resultVeterano = await _meetingService.GetMeetingByIdAsync(agoMeeting.Id, _veteranoUser.Id);
        var resultNonVeterano = await _meetingService.GetMeetingByIdAsync(agoMeeting.Id, _nonVeteranoUser.Id);

        // Assert
        resultVeterano.Should().NotBeNull();
        resultNonVeterano.Should().NotBeNull();
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
        var deleted = await _context.Meetings.FindAsync(meetingId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMeetingAsync_NonExistingMeeting_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _meetingService.DeleteMeetingAsync(999));
    }

    [Fact]
    public async Task GetTotalCountAsync_NonVeteranoUser_ExcludesCVMeetings()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO 1",
            Date = DateTime.Now.AddDays(7),
            Statement = "Statement 1"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO 2",
            Date = DateTime.Now.AddDays(8),
            Statement = "Statement 2"
        });

        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.Now.AddDays(9),
            Statement = "CV Statement"
        });

        await _context.SaveChangesAsync();

        // Act
        var count = await _meetingService.GetTotalCountAsync(null, _nonVeteranoUser.Id);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetTotalCountAsync_VeteranoUser_IncludesCVMeetings()
    {
        // Arrange
        _context.Meetings.Add(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO 1",
            Date = DateTime.Now.AddDays(7),
            Statement = "Statement 1"
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
        var count = await _meetingService.GetTotalCountAsync(null, _veteranoUser.Id);

        // Assert
        count.Should().Be(2);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
