using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class MeetingRequestTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var request = new MeetingRequest();

        // Assert
        request.Should().NotBeNull();
        request.Title.Should().Be(string.Empty);
        request.Description.Should().Be(string.Empty);
        request.AuthorUserId.Should().Be(string.Empty);
        request.Status.Should().Be(RequestStatus.Pending);
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var proposedDateTime = new DateTime(2024, 7, 20, 18, 0, 0, DateTimeKind.Utc);
        var request = new MeetingRequest
        {
            Id = 1,
            RequestedMeetingType = MeetingType.ConselhoVeteranos,
            Title = "Solicitação de Conselho de Veteranos",
            ProposedDateTime = proposedDateTime,
            Location = "Sede",
            Description = "Discussão sobre estatutos",
            Status = RequestStatus.Pending,
            AuthorUserId = "author-123"
        };

        // Assert
        request.Id.Should().Be(1);
        request.RequestedMeetingType.Should().Be(MeetingType.ConselhoVeteranos);
        request.Title.Should().Be("Solicitação de Conselho de Veteranos");
        request.ProposedDateTime.Should().Be(proposedDateTime);
        request.Location.Should().Be("Sede");
        request.Description.Should().Be("Discussão sobre estatutos");
        request.Status.Should().Be(RequestStatus.Pending);
        request.AuthorUserId.Should().Be("author-123");
    }

    [Theory]
    [InlineData(MeetingType.AssembleiaGeralOrdinaria)]
    [InlineData(MeetingType.AssembleiaGeralExtraordinaria)]
    [InlineData(MeetingType.ConselhoVeteranos)]
    public void RequestedMeetingType_WithDifferentTypes_ShouldStore(MeetingType type)
    {
        // Arrange
        var request = new MeetingRequest { RequestedMeetingType = type };

        // Assert
        request.RequestedMeetingType.Should().Be(type);
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Analysing)]
    [InlineData(RequestStatus.Confirmed)]
    [InlineData(RequestStatus.Rejected)]
    public void Status_WithDifferentStatuses_ShouldStore(RequestStatus status)
    {
        // Arrange
        var request = new MeetingRequest { Status = status };

        // Assert
        request.Status.Should().Be(status);
    }

    [Fact]
    public void Location_WhenNull_ShouldAllowNullValue()
    {
        // Arrange
        var request = new MeetingRequest
        {
            Title = "Virtual Meeting Request",
            Location = null
        };

        // Assert
        request.Location.Should().BeNull();
    }

    [Fact]
    public void Author_NavigationProperty_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var request = new MeetingRequest();

        // Assert
        request.Author.Should().BeNull();
    }

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = new MeetingRequest
        {
            CreatedAt = now,
            CreatedBy = "creator",
            UpdatedAt = now.AddHours(1),
            UpdatedBy = "updater"
        };

        // Assert
        request.CreatedAt.Should().Be(now);
        request.CreatedBy.Should().Be("creator");
        request.UpdatedAt.Should().Be(now.AddHours(1));
        request.UpdatedBy.Should().Be("updater");
    }
}
