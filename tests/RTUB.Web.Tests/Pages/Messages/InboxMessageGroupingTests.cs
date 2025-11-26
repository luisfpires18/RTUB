using Xunit;
using FluentAssertions;
using RTUB.Application.DTOs;

namespace RTUB.Web.Tests.Pages.Messages;

/// <summary>
/// Unit tests for the Inbox page message grouping behavior.
/// Tests the visual grouping of consecutive messages from the same sender.
/// </summary>
public class InboxMessageGroupingTests
{
    /// <summary>
    /// Enum representing the position of a message within a group of consecutive messages from the same sender.
    /// This mirrors the private enum in Inbox.razor for testing purposes.
    /// </summary>
    private enum MessageGroupPosition
    {
        Single,  // Only message in the group
        First,   // First message in a group
        Middle,  // Middle message in a group
        Last     // Last message in a group
    }

    /// <summary>
    /// Helper method that mirrors GetMessageGroupPosition from Inbox.razor
    /// </summary>
    private static MessageGroupPosition GetMessageGroupPosition(List<MessageDto> messages, int index)
    {
        if (index < 0 || index >= messages.Count)
        {
            return MessageGroupPosition.Single;
        }

        var currentMessage = messages[index];
        var previousMessage = index > 0 ? messages[index - 1] : null;
        var nextMessage = index < messages.Count - 1 ? messages[index + 1] : null;

        var hasSamePreviousSender = previousMessage != null && previousMessage.SenderId == currentMessage.SenderId;
        var hasSameNextSender = nextMessage != null && nextMessage.SenderId == currentMessage.SenderId;

        if (!hasSamePreviousSender && !hasSameNextSender)
        {
            return MessageGroupPosition.Single;
        }

        if (!hasSamePreviousSender && hasSameNextSender)
        {
            return MessageGroupPosition.First;
        }

        if (hasSamePreviousSender && hasSameNextSender)
        {
            return MessageGroupPosition.Middle;
        }

        // hasSamePreviousSender && !hasSameNextSender
        return MessageGroupPosition.Last;
    }

    /// <summary>
    /// Helper method that mirrors GetMessageGroupClass from Inbox.razor
    /// </summary>
    private static string GetMessageGroupClass(MessageGroupPosition position)
    {
        return position switch
        {
            MessageGroupPosition.Single => "message-group-single",
            MessageGroupPosition.First => "message-group-first",
            MessageGroupPosition.Middle => "message-group-middle",
            MessageGroupPosition.Last => "message-group-last",
            _ => string.Empty
        };
    }

    #region Single Message Tests

    [Fact]
    public void GetMessageGroupPosition_SingleMessage_ReturnsPosition_Single()
    {
        // Arrange
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 0);

        // Assert
        position.Should().Be(MessageGroupPosition.Single);
    }

    [Fact]
    public void GetMessageGroupPosition_OnlyMessageFromDifferentSenders_ReturnsPosition_Single()
    {
        // Arrange - Three messages from different senders
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user2", Body = "Hi" },
            new MessageDto { Id = 3, SenderId = "user3", Body = "Hey" }
        };

        // Act & Assert
        GetMessageGroupPosition(messages, 0).Should().Be(MessageGroupPosition.Single);
        GetMessageGroupPosition(messages, 1).Should().Be(MessageGroupPosition.Single);
        GetMessageGroupPosition(messages, 2).Should().Be(MessageGroupPosition.Single);
    }

    #endregion

    #region First Position Tests

    [Fact]
    public void GetMessageGroupPosition_FirstOfTwoConsecutive_ReturnsPosition_First()
    {
        // Arrange - Two consecutive messages from the same sender
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "How are you?" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 0);

        // Assert
        position.Should().Be(MessageGroupPosition.First);
    }

    [Fact]
    public void GetMessageGroupPosition_FirstOfMultipleConsecutive_ReturnsPosition_First()
    {
        // Arrange - Three consecutive messages from the same sender
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "How are you?" },
            new MessageDto { Id = 3, SenderId = "user1", Body = "Anyone there?" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 0);

        // Assert
        position.Should().Be(MessageGroupPosition.First);
    }

    #endregion

    #region Middle Position Tests

    [Fact]
    public void GetMessageGroupPosition_MiddleOfThreeConsecutive_ReturnsPosition_Middle()
    {
        // Arrange - Three consecutive messages from the same sender
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "How are you?" },
            new MessageDto { Id = 3, SenderId = "user1", Body = "Anyone there?" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 1);

        // Assert
        position.Should().Be(MessageGroupPosition.Middle);
    }

    [Fact]
    public void GetMessageGroupPosition_MiddleOfFourConsecutive_AllMiddlesAre_Middle()
    {
        // Arrange - Four consecutive messages from the same sender
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Message 1" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "Message 2" },
            new MessageDto { Id = 3, SenderId = "user1", Body = "Message 3" },
            new MessageDto { Id = 4, SenderId = "user1", Body = "Message 4" }
        };

        // Act & Assert
        GetMessageGroupPosition(messages, 1).Should().Be(MessageGroupPosition.Middle);
        GetMessageGroupPosition(messages, 2).Should().Be(MessageGroupPosition.Middle);
    }

    #endregion

    #region Last Position Tests

    [Fact]
    public void GetMessageGroupPosition_LastOfTwoConsecutive_ReturnsPosition_Last()
    {
        // Arrange - Two consecutive messages from the same sender
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "How are you?" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 1);

        // Assert
        position.Should().Be(MessageGroupPosition.Last);
    }

    [Fact]
    public void GetMessageGroupPosition_LastOfMultipleConsecutive_ReturnsPosition_Last()
    {
        // Arrange - Three consecutive messages from the same sender
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "How are you?" },
            new MessageDto { Id = 3, SenderId = "user1", Body = "Anyone there?" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 2);

        // Assert
        position.Should().Be(MessageGroupPosition.Last);
    }

    #endregion

    #region Mixed Conversation Tests

    [Fact]
    public void GetMessageGroupPosition_AlternatingMessages_AllAre_Single()
    {
        // Arrange - Alternating messages between two users
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },
            new MessageDto { Id = 2, SenderId = "user2", Body = "Hi!" },
            new MessageDto { Id = 3, SenderId = "user1", Body = "How are you?" },
            new MessageDto { Id = 4, SenderId = "user2", Body = "Good, thanks!" }
        };

        // Act & Assert - All should be single since they alternate
        GetMessageGroupPosition(messages, 0).Should().Be(MessageGroupPosition.Single);
        GetMessageGroupPosition(messages, 1).Should().Be(MessageGroupPosition.Single);
        GetMessageGroupPosition(messages, 2).Should().Be(MessageGroupPosition.Single);
        GetMessageGroupPosition(messages, 3).Should().Be(MessageGroupPosition.Single);
    }

    [Fact]
    public void GetMessageGroupPosition_MixedConversation_CorrectlyIdentifiesGroups()
    {
        // Arrange - Mixed conversation with groups
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" },        // First of group
            new MessageDto { Id = 2, SenderId = "user1", Body = "How are you?" }, // Last of group
            new MessageDto { Id = 3, SenderId = "user2", Body = "Hi!" },          // Single
            new MessageDto { Id = 4, SenderId = "user1", Body = "Good to hear" }, // First of group
            new MessageDto { Id = 5, SenderId = "user1", Body = "Let's meet" },   // Middle of group
            new MessageDto { Id = 6, SenderId = "user1", Body = "Tomorrow?" }     // Last of group
        };

        // Act & Assert
        GetMessageGroupPosition(messages, 0).Should().Be(MessageGroupPosition.First, "First message of first group");
        GetMessageGroupPosition(messages, 1).Should().Be(MessageGroupPosition.Last, "Last message of first group");
        GetMessageGroupPosition(messages, 2).Should().Be(MessageGroupPosition.Single, "Single message from user2");
        GetMessageGroupPosition(messages, 3).Should().Be(MessageGroupPosition.First, "First message of second group");
        GetMessageGroupPosition(messages, 4).Should().Be(MessageGroupPosition.Middle, "Middle message of second group");
        GetMessageGroupPosition(messages, 5).Should().Be(MessageGroupPosition.Last, "Last message of second group");
    }

    [Fact]
    public void GetMessageGroupPosition_ConsecutiveGroupsFromSameSender_SeparatedByOther()
    {
        // Arrange - Same sender has two separate groups
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Group 1 - Message 1" },
            new MessageDto { Id = 2, SenderId = "user1", Body = "Group 1 - Message 2" },
            new MessageDto { Id = 3, SenderId = "user2", Body = "Interruption" },
            new MessageDto { Id = 4, SenderId = "user1", Body = "Group 2 - Message 1" },
            new MessageDto { Id = 5, SenderId = "user1", Body = "Group 2 - Message 2" }
        };

        // Act & Assert
        GetMessageGroupPosition(messages, 0).Should().Be(MessageGroupPosition.First, "First message of first user1 group");
        GetMessageGroupPosition(messages, 1).Should().Be(MessageGroupPosition.Last, "Last message of first user1 group");
        GetMessageGroupPosition(messages, 2).Should().Be(MessageGroupPosition.Single, "Single message interruption");
        GetMessageGroupPosition(messages, 3).Should().Be(MessageGroupPosition.First, "First message of second user1 group");
        GetMessageGroupPosition(messages, 4).Should().Be(MessageGroupPosition.Last, "Last message of second user1 group");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void GetMessageGroupPosition_EmptyList_ReturnsSingle()
    {
        // Arrange
        var messages = new List<MessageDto>();

        // Act
        var position = GetMessageGroupPosition(messages, 0);

        // Assert
        position.Should().Be(MessageGroupPosition.Single);
    }

    [Fact]
    public void GetMessageGroupPosition_NegativeIndex_ReturnsSingle()
    {
        // Arrange
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, -1);

        // Assert
        position.Should().Be(MessageGroupPosition.Single);
    }

    [Fact]
    public void GetMessageGroupPosition_IndexOutOfRange_ReturnsSingle()
    {
        // Arrange
        var messages = new List<MessageDto>
        {
            new MessageDto { Id = 1, SenderId = "user1", Body = "Hello" }
        };

        // Act
        var position = GetMessageGroupPosition(messages, 5);

        // Assert
        position.Should().Be(MessageGroupPosition.Single);
    }

    #endregion

    #region CSS Class Tests

    [Fact]
    public void GetMessageGroupClass_SinglePosition_ReturnsCorrectClass()
    {
        // Act
        var cssClass = GetMessageGroupClass(MessageGroupPosition.Single);

        // Assert
        cssClass.Should().Be("message-group-single");
    }

    [Fact]
    public void GetMessageGroupClass_FirstPosition_ReturnsCorrectClass()
    {
        // Act
        var cssClass = GetMessageGroupClass(MessageGroupPosition.First);

        // Assert
        cssClass.Should().Be("message-group-first");
    }

    [Fact]
    public void GetMessageGroupClass_MiddlePosition_ReturnsCorrectClass()
    {
        // Act
        var cssClass = GetMessageGroupClass(MessageGroupPosition.Middle);

        // Assert
        cssClass.Should().Be("message-group-middle");
    }

    [Fact]
    public void GetMessageGroupClass_LastPosition_ReturnsCorrectClass()
    {
        // Act
        var cssClass = GetMessageGroupClass(MessageGroupPosition.Last);

        // Assert
        cssClass.Should().Be("message-group-last");
    }

    #endregion

    #region Avatar/Timestamp Visibility Tests

    [Fact]
    public void AvatarVisibility_FirstPosition_ShouldShowAvatar()
    {
        // Arrange
        var position = MessageGroupPosition.First;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;

        // Assert
        showAvatar.Should().BeTrue("Avatar should be visible on first message of a group");
    }

    [Fact]
    public void AvatarVisibility_SinglePosition_ShouldShowAvatar()
    {
        // Arrange
        var position = MessageGroupPosition.Single;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;

        // Assert
        showAvatar.Should().BeTrue("Avatar should be visible on single messages");
    }

    [Fact]
    public void AvatarVisibility_MiddlePosition_ShouldHideAvatar()
    {
        // Arrange
        var position = MessageGroupPosition.Middle;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;

        // Assert
        showAvatar.Should().BeFalse("Avatar should be hidden on middle messages of a group");
    }

    [Fact]
    public void AvatarVisibility_LastPosition_ShouldHideAvatar()
    {
        // Arrange
        var position = MessageGroupPosition.Last;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;

        // Assert
        showAvatar.Should().BeFalse("Avatar should be hidden on last message of a group");
    }

    [Fact]
    public void TimestampVisibility_LastPosition_ShouldShowTimestamp()
    {
        // Arrange
        var position = MessageGroupPosition.Last;

        // Act
        var showTimestamp = position == MessageGroupPosition.Last || position == MessageGroupPosition.Single;

        // Assert
        showTimestamp.Should().BeTrue("Timestamp should be visible on last message of a group");
    }

    [Fact]
    public void TimestampVisibility_SinglePosition_ShouldShowTimestamp()
    {
        // Arrange
        var position = MessageGroupPosition.Single;

        // Act
        var showTimestamp = position == MessageGroupPosition.Last || position == MessageGroupPosition.Single;

        // Assert
        showTimestamp.Should().BeTrue("Timestamp should be visible on single messages");
    }

    [Fact]
    public void TimestampVisibility_FirstPosition_ShouldHideTimestamp()
    {
        // Arrange
        var position = MessageGroupPosition.First;

        // Act
        var showTimestamp = position == MessageGroupPosition.Last || position == MessageGroupPosition.Single;

        // Assert
        showTimestamp.Should().BeFalse("Timestamp should be hidden on first message of a group");
    }

    [Fact]
    public void TimestampVisibility_MiddlePosition_ShouldHideTimestamp()
    {
        // Arrange
        var position = MessageGroupPosition.Middle;

        // Act
        var showTimestamp = position == MessageGroupPosition.Last || position == MessageGroupPosition.Single;

        // Assert
        showTimestamp.Should().BeFalse("Timestamp should be hidden on middle messages of a group");
    }

    #endregion

    #region Sender Name Visibility Tests

    [Fact]
    public void SenderNameVisibility_InGroupChat_FirstPosition_ShouldShowSenderName()
    {
        // Arrange
        var position = MessageGroupPosition.First;
        var isGroupChat = true;
        var isCurrentUser = false;
        var isSystemMessage = false;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;
        var showSenderName = showAvatar && isGroupChat && !isCurrentUser && !isSystemMessage;

        // Assert
        showSenderName.Should().BeTrue("Sender name should be visible on first message in group chat");
    }

    [Fact]
    public void SenderNameVisibility_InGroupChat_MiddlePosition_ShouldHideSenderName()
    {
        // Arrange
        var position = MessageGroupPosition.Middle;
        var isGroupChat = true;
        var isCurrentUser = false;
        var isSystemMessage = false;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;
        var showSenderName = showAvatar && isGroupChat && !isCurrentUser && !isSystemMessage;

        // Assert
        showSenderName.Should().BeFalse("Sender name should be hidden on middle messages in group chat");
    }

    [Fact]
    public void SenderNameVisibility_InDirectMessage_FirstPosition_ShouldHideSenderName()
    {
        // Arrange
        var position = MessageGroupPosition.First;
        var isGroupChat = false; // Direct message, not group
        var isCurrentUser = false;
        var isSystemMessage = false;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;
        var showSenderName = showAvatar && isGroupChat && !isCurrentUser && !isSystemMessage;

        // Assert
        showSenderName.Should().BeFalse("Sender name should be hidden in direct messages");
    }

    [Fact]
    public void SenderNameVisibility_CurrentUserMessage_ShouldAlwaysHideSenderName()
    {
        // Arrange
        var position = MessageGroupPosition.First;
        var isGroupChat = true;
        var isCurrentUser = true; // Message from current user
        var isSystemMessage = false;

        // Act
        var showAvatar = position == MessageGroupPosition.First || position == MessageGroupPosition.Single;
        var showSenderName = showAvatar && isGroupChat && !isCurrentUser && !isSystemMessage;

        // Assert
        showSenderName.Should().BeFalse("Sender name should never be shown for current user's messages");
    }

    #endregion
}
