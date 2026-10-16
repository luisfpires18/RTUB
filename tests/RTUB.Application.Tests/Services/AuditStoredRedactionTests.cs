using FluentAssertions;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>#226 F04: hiding redacted properties of entries written before the rule, at read time only.</summary>
public class AuditStoredRedactionTests
{
    [Fact]
    public void OldPushKeys_AreHidden_EverythingElseKept()
    {
        var shown = AuditLogAppender.RedactStored("PushSubscription",
            "{\"Auth\":\"a\",\"Endpoint\":\"https://push.example/x\",\"P256dh\":\"p\",\"UserAgent\":\"Edge\"}");
        shown.Should().Be("{\"Auth\":\"[redacted]\",\"Endpoint\":\"[redacted]\",\"P256dh\":\"[redacted]\",\"UserAgent\":\"Edge\"}");
    }

    [Theory]
    [InlineData("PushSubscription", "{\"Auth\":\"[redacted]\",\"UserAgent\":\"caf\\u00E9\"}")] // already redacted: as stored, byte for byte
    [InlineData("PushSubscription", "Deleted from a sentence, not JSON")]
    [InlineData("PushSubscription", "[1,2]")]
    [InlineData("Event", "{\"Auth\":\"not a push key\"}")]
    [InlineData(null, "{\"Auth\":\"x\"}")]
    [InlineData("PushSubscription", null)]
    public void NothingToHide_ReturnsTheStoredText(string? type, string? changes) =>
        AuditLogAppender.RedactStored(type, changes).Should().Be(changes);

    [Fact]
    public void MessagingContent_FollowsTheSameRule()
    {
        AuditLogAppender.RedactStored("Message", "{\"Body\":\"olá\",\"ConversationId\":3}")
            .Should().NotContain("ol").And.Contain("[redacted]").And.Contain("\"ConversationId\":3");
    }
}
