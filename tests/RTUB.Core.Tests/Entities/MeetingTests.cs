using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class MeetingTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var meeting = new Meeting();

        // Assert
        meeting.Should().NotBeNull();
        meeting.Title.Should().Be(string.Empty);
        meeting.Statement.Should().Be(string.Empty);
        meeting.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var date = new DateTime(2024, 6, 15, 18, 0, 0, DateTimeKind.Utc);
        var meeting = new Meeting
        {
            Id = 1,
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Assembleia Geral Ordinária 2024",
            Date = date,
            Location = "Sala de Reuniões",
            Statement = "Ordem de trabalhos da assembleia",
            OrganizerUserId = "organizer-123",
            IsCancelled = false,
            CancellationReason = null
        };

        // Assert
        meeting.Id.Should().Be(1);
        meeting.Type.Should().Be(MeetingType.AssembleiaGeralOrdinaria);
        meeting.Title.Should().Be("Assembleia Geral Ordinária 2024");
        meeting.Date.Should().Be(date);
        meeting.Location.Should().Be("Sala de Reuniões");
        meeting.Statement.Should().Be("Ordem de trabalhos da assembleia");
        meeting.OrganizerUserId.Should().Be("organizer-123");
        meeting.IsCancelled.Should().BeFalse();
        meeting.CancellationReason.Should().BeNull();
    }

    [Theory]
    [InlineData(MeetingType.AssembleiaGeralOrdinaria)]
    [InlineData(MeetingType.AssembleiaGeralExtraordinaria)]
    [InlineData(MeetingType.ConselhoVeteranos)]
    public void Type_WithDifferentMeetingTypes_ShouldStore(MeetingType type)
    {
        // Arrange
        var meeting = new Meeting { Type = type };

        // Assert
        meeting.Type.Should().Be(type);
    }

    [Fact]
    public void IsCancelled_WhenSetToTrue_ShouldBeTrue()
    {
        // Arrange
        var meeting = new Meeting
        {
            IsCancelled = true,
            CancellationReason = "Lack of quorum"
        };

        // Assert
        meeting.IsCancelled.Should().BeTrue();
        meeting.CancellationReason.Should().Be("Lack of quorum");
    }

    [Fact]
    public void Location_WhenNull_ShouldAllowNullValue()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Virtual Meeting",
            Location = null
        };

        // Assert
        meeting.Location.Should().BeNull();
    }

    [Fact]
    public void OrganizerUserId_WhenNull_ShouldAllowNullValue()
    {
        // Arrange
        var meeting = new Meeting
        {
            OrganizerUserId = null
        };

        // Assert
        meeting.OrganizerUserId.Should().BeNull();
    }

    [Fact]
    public void Organizer_NavigationProperty_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var meeting = new Meeting();

        // Assert
        meeting.Organizer.Should().BeNull();
    }

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var meeting = new Meeting
        {
            CreatedAt = now,
            CreatedBy = "creator",
            UpdatedAt = now.AddHours(1),
            UpdatedBy = "updater"
        };

        // Assert
        meeting.CreatedAt.Should().Be(now);
        meeting.CreatedBy.Should().Be("creator");
        meeting.UpdatedAt.Should().Be(now.AddHours(1));
        meeting.UpdatedBy.Should().Be("updater");
    }
}
