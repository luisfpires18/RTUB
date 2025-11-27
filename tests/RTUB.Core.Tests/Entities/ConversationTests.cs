using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class ConversationTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var conversation = new Conversation();

        // Assert
        conversation.Should().NotBeNull();
        conversation.Participants.Should().Be(string.Empty);
        conversation.IsSystemConversation.Should().BeFalse();
        conversation.IsArchived.Should().BeFalse();
        conversation.IsGroup.Should().BeFalse();
        conversation.IsAnnouncementOnly.Should().BeFalse();
        conversation.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Participants = "user1;user2",
            LastMessageAt = now,
            LastMessageId = 42,
            Title = "Test Conversation",
            IsSystemConversation = true,
            IsArchived = true,
            IsGroup = true,
            CreatedByUserId = "creator-id",
            IsAnnouncementOnly = true
        };

        // Assert
        conversation.Participants.Should().Be("user1;user2");
        conversation.LastMessageAt.Should().Be(now);
        conversation.LastMessageId.Should().Be(42);
        conversation.Title.Should().Be("Test Conversation");
        conversation.IsSystemConversation.Should().BeTrue();
        conversation.IsArchived.Should().BeTrue();
        conversation.IsGroup.Should().BeTrue();
        conversation.CreatedByUserId.Should().Be("creator-id");
        conversation.IsAnnouncementOnly.Should().BeTrue();
    }

    #region GetParticipantIds Tests

    [Fact]
    public void GetParticipantIds_WithEmptyParticipants_ShouldReturnEmptyList()
    {
        // Arrange
        var conversation = new Conversation { Participants = "" };

        // Act
        var result = conversation.GetParticipantIds();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetParticipantIds_WithSingleParticipant_ShouldReturnSingleItem()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1" };

        // Act
        var result = conversation.GetParticipantIds();

        // Assert
        result.Should().ContainSingle().Which.Should().Be("user1");
    }

    [Fact]
    public void GetParticipantIds_WithMultipleParticipants_ShouldReturnAllParticipants()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3" };

        // Act
        var result = conversation.GetParticipantIds();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(new[] { "user1", "user2", "user3" });
    }

    [Fact]
    public void GetParticipantIds_WithTrailingSemicolon_ShouldHandleCorrectly()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;" };

        // Act
        var result = conversation.GetParticipantIds();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { "user1", "user2" });
    }

    #endregion

    #region GetOtherParticipantId Tests

    [Fact]
    public void GetOtherParticipantId_InOneToOneConversation_ShouldReturnOtherParticipant()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        var result = conversation.GetOtherParticipantId("user1");

        // Assert
        result.Should().Be("user2");
    }

    [Fact]
    public void GetOtherParticipantId_WhenUserNotInConversation_ShouldReturnFirstParticipant()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        var result = conversation.GetOtherParticipantId("user3");

        // Assert
        result.Should().Be("user1");
    }

    [Fact]
    public void GetOtherParticipantId_WithEmptyParticipants_ShouldReturnNull()
    {
        // Arrange
        var conversation = new Conversation { Participants = "" };

        // Act
        var result = conversation.GetOtherParticipantId("user1");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region HasParticipant Tests

    [Fact]
    public void HasParticipant_WhenUserIsParticipant_ShouldReturnTrue()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3" };

        // Act
        var result = conversation.HasParticipant("user2");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasParticipant_WhenUserIsNotParticipant_ShouldReturnFalse()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        var result = conversation.HasParticipant("user99");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasParticipant_WithEmptyParticipants_ShouldReturnFalse()
    {
        // Arrange
        var conversation = new Conversation { Participants = "" };

        // Act
        var result = conversation.HasParticipant("user1");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AddParticipant Tests

    [Fact]
    public void AddParticipant_ToEmptyConversation_ShouldAddParticipant()
    {
        // Arrange
        var conversation = new Conversation { Participants = "" };

        // Act
        conversation.AddParticipant("user1");

        // Assert
        conversation.Participants.Should().Be("user1");
    }

    [Fact]
    public void AddParticipant_ToExistingConversation_ShouldAppendParticipant()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1" };

        // Act
        conversation.AddParticipant("user2");

        // Assert
        conversation.Participants.Should().Be("user1;user2");
    }

    [Fact]
    public void AddParticipant_WhenAlreadyParticipant_ShouldNotDuplicate()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        conversation.AddParticipant("user1");

        // Assert
        conversation.Participants.Should().Be("user1;user2");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddParticipant_WithEmptyOrNullUserId_ShouldNotAddParticipant(string? userId)
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1" };

        // Act
        conversation.AddParticipant(userId!);

        // Assert
        conversation.Participants.Should().Be("user1");
    }

    #endregion

    #region RemoveParticipant Tests

    [Fact]
    public void RemoveParticipant_WhenParticipantExists_ShouldRemove()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3" };

        // Act
        conversation.RemoveParticipant("user2");

        // Assert
        conversation.Participants.Should().Be("user1;user3");
    }

    [Fact]
    public void RemoveParticipant_WhenParticipantNotExists_ShouldNotChange()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        conversation.RemoveParticipant("user99");

        // Assert
        conversation.Participants.Should().Be("user1;user2");
    }

    [Fact]
    public void RemoveParticipant_WithEmptyUserId_ShouldNotChange()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        conversation.RemoveParticipant("");

        // Assert
        conversation.Participants.Should().Be("user1;user2");
    }

    [Fact]
    public void RemoveParticipant_LastParticipant_ShouldResultInEmptyString()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1" };

        // Act
        conversation.RemoveParticipant("user1");

        // Assert
        conversation.Participants.Should().Be("");
    }

    #endregion

    #region SetParticipants Tests

    [Fact]
    public void SetParticipants_WithNewList_ShouldReplaceAll()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };
        var newParticipants = new[] { "user3", "user4", "user5" };

        // Act
        conversation.SetParticipants(newParticipants);

        // Assert
        conversation.Participants.Should().Be("user3;user4;user5");
    }

    [Fact]
    public void SetParticipants_WithDuplicates_ShouldRemoveDuplicates()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1" };
        var newParticipants = new[] { "user1", "user2", "user1", "user3", "user2" };

        // Act
        conversation.SetParticipants(newParticipants);

        // Assert
        var participantIds = conversation.GetParticipantIds();
        participantIds.Should().HaveCount(3);
        participantIds.Should().Contain(new[] { "user1", "user2", "user3" });
    }

    [Fact]
    public void SetParticipants_WithEmptyList_ShouldClearParticipants()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2" };

        // Act
        conversation.SetParticipants(Array.Empty<string>());

        // Assert
        conversation.Participants.Should().Be("");
    }

    #endregion
}
