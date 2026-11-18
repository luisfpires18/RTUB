// Conversation set participants
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class ConversationTests
{
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
