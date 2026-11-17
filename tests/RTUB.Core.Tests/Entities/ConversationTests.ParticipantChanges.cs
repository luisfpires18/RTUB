// Conversation add, remove and set participants
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class ConversationTests
{
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
