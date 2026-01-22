using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingService
/// Tests business logic for meeting operations and Veterano visibility filtering
/// Uses shared database fixture for better performance
/// </summary>
[Collection("MeetingService Collection")]
public class MeetingServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MeetingService _meetingService;
    private readonly ApplicationUser _veteranoUser;
    private readonly ApplicationUser _tunossauroUser;
    private readonly ApplicationUser _nonVeteranoUser;
    private readonly ApplicationUser _leitaoUser;

    public MeetingServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();

        // Create mocks for new dependencies
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _meetingService = new MeetingService(
            new MeetingRepository(_context),
            _context,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Disable auditing for test setup
        _context.DisableAuditing();

        // Create test users
        var currentYear = DateTime.Now.Year;
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
            PhoneNumber = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Veterano },
            YearTuno = currentYear - 3,  // Started 3 years ago (>= 2 years makes them Veterano)
            MonthTuno = 1
        };

        _tunossauroUser = new ApplicationUser
        {
            Id = "tunossauro-user-id",
            UserName = "tunossauro@test.com",
            NormalizedUserName = "TUNOSSAURO@TEST.COM",
            Email = "tunossauro@test.com",
            NormalizedEmail = "TUNOSSAURO@TEST.COM",
            FirstName = "Tunossauro",
            LastName = "User",
            Nickname = "TunoTest",
            PhoneNumber = "111222333",
            Categories = new List<MemberCategory> { MemberCategory.Tunossauro },
            YearTuno = currentYear - 7,  // Started 7 years ago (>= 6 years makes them Tunossauro)
            MonthTuno = 1
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
            PhoneNumber = "987654321",
            Categories = new List<MemberCategory> { MemberCategory.Tuno },
            YearTuno = currentYear - 1,  // Started 1 year ago (< 2 years makes them Tuno)
            MonthTuno = 1
        };

        _leitaoUser = new ApplicationUser
        {
            Id = "leitao-user-id",
            UserName = "leitao@test.com",
            NormalizedUserName = "LEITAO@TEST.COM",
            Email = "leitao@test.com",
            NormalizedEmail = "LEITAO@TEST.COM",
            FirstName = "Leitao",
            LastName = "User",
            Nickname = "LeitaoTest",
            PhoneNumber = "555666777",
            Categories = new List<MemberCategory> { MemberCategory.Leitao }
        };

        // Only add users if they don't already exist (for shared database with multiple test runs)
        if (!_context.Users.Any(u => u.Id == _veteranoUser.Id))
        {
            _context.Users.Add(_veteranoUser);
            _context.Users.Add(_tunossauroUser);
            _context.Users.Add(_nonVeteranoUser);
            _context.Users.Add(_leitaoUser);
            _context.SaveChanges();
        }

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
    public async Task GetMeetingByIdAsync_CVMeeting_TunossauroUser_ReturnsMeeting()
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
        var result = await _meetingService.GetMeetingByIdAsync(cvMeeting.Id, _tunossauroUser.Id);

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
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
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

    [Fact]
    public async Task GetMeetingByIdAsync_AGOMeeting_LeitaoUser_ReturnsNull()
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
        var result = await _meetingService.GetMeetingByIdAsync(agoMeeting.Id, _leitaoUser.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMeetingByIdAsync_AGEMeeting_LeitaoUser_ReturnsNull()
    {
        // Arrange
        var ageMeeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "AGE Meeting",
            Date = DateTime.Now.AddDays(7),
            Statement = "AGE Statement"
        };
        _context.Meetings.Add(ageMeeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetMeetingByIdAsync(ageMeeting.Id, _leitaoUser.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTotalCountAsync_LeitaoUser_ExcludesAssembleiaGeralMeetings()
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
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "AGE 1",
            Date = DateTime.Now.AddDays(8),
            Statement = "Statement 2"
        });

        await _context.SaveChangesAsync();

        // Act
        var count = await _meetingService.GetTotalCountAsync(null, _leitaoUser.Id);

        // Assert
        count.Should().Be(0); // Leitão can't see AG meetings
    }

    public void Dispose()
    {
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
