using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

// AuditLogAppenderTests: Audit Log For User tests.
public partial class AuditLogAppenderTests
{
    [Fact]
    public void CreateAuditLogForUser_WhenUserModified_ReturnsAuditLog()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "targetuser",
            Email = "target@example.com",
            FirstName = "Target",
            LastName = "User",
            Nickname = "TargetUser"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        user.Nickname = "UpdatedNickname";
        _context.Users.Update(user);
        var entry = _context.Entry(user);

        // Act
        var auditLog = _appender.CreateAuditLogForUser(
            entry,
            "Modified",
            _testUsername,
            _testUserId);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.Changes.Should().NotBeNull();

        var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Changes!);
        changes.Should().ContainKey("_TargetUser");
        changes.Should().ContainKey("Nickname");
    }

    [Fact]
    public void CreateAuditLogForUser_WhenUserDeleted_MarksAsCritical()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "targetuser",
            Email = "target@example.com",
            FirstName = "Target",
            LastName = "User",
            Nickname = "TargetUser"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _context.Users.Remove(user);
        var entry = _context.Entry(user);

        // Act
        var auditLog = _appender.CreateAuditLogForUser(
            entry,
            "Deleted",
            _testUsername,
            _testUserId);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.IsCriticalAction.Should().BeTrue(); // User deletions are always critical
        auditLog.Action.Should().Be("Deleted");
    }

    [Fact]
    public void CreateAuditLogForUser_WhenPasswordChanged_MarksAsCritical()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "targetuser",
            Email = "target@example.com",
            FirstName = "Target",
            LastName = "User",
            Nickname = "TargetUser",
            PasswordHash = "oldhash"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        user.PasswordHash = "newhash";
        _context.Users.Update(user);
        var entry = _context.Entry(user);

        // Act
        var auditLog = _appender.CreateAuditLogForUser(
            entry,
            "Modified",
            _testUsername,
            _testUserId);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.IsCriticalAction.Should().BeTrue(); // Password changes are critical
        auditLog.Changes.Should().NotBeNull();

        var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Changes!);
        changes.Should().ContainKey("_CriticalFieldsModified");
    }

    [Fact]
    public void CreateAuditLog_ExcludesMetadataFields()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        _context.SaveChanges();

        album.UpdateDetails("Updated Album", 2025, "Updated description", false);
        _context.Albums.Update(album);
        var entry = _context.Entry((BaseEntity)album);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Modified",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog!.Changes!);
        changes.Should().NotContainKey("CreatedAt");
        changes.Should().NotContainKey("CreatedBy");
        changes.Should().NotContainKey("UpdatedAt");
        changes.Should().NotContainKey("UpdatedBy");
        changes.Should().NotContainKey("Id");
    }

    [Fact]
    public void CreateAuditLog_HandlesBinaryData()
    {
        // Arrange - Use Report with PdfData (binary property) instead
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        _context.SaveChanges();

        var binaryData = new byte[1000];
        report.PdfData = binaryData;
        _context.Reports.Update(report);
        var entry = _context.Entry((BaseEntity)report);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Modified",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.Changes.Should().NotBeNull();

        var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Changes!);
        changes.Should().ContainKey("PdfData");

        // Verify binary data is described, not serialized
        var pdfDataChange = changes["PdfData"];
        pdfDataChange.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("Deleted")]
    public void CreateAuditLog_ForAMessage_RedactsItsContent_EvenIfItWereAudited(string action)
    {
        const string secret = "texto privado da conversa";
        var message = new Message
        {
            Id = 7, ConversationId = 3, SenderId = "sender-id", Body = secret, ReplyToBody = secret, ReplyToSenderName = "Fulano",
            ReadBy = "a;b", Link = "/events/1",
        };
        _context.Messages.Add(message);
        var entry = _context.Entry((BaseEntity)message);
        if (action == "Deleted")
        {
            entry.State = EntityState.Deleted;
        }

        var auditLog = _appender.CreateAuditLog(entry, action, _testUsername, _testUserId, ResolveUserIdToNickname, GetEntityDisplayName);

        auditLog!.Changes.Should().NotContain(secret).And.NotContain("Fulano").And.NotContain("/events/1");
        var changes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(auditLog.Changes!)!;
        foreach (var field in new[] { "Body", "ReplyToBody", "ReplyToSenderName", "ReadBy", "Link" })
        {
            changes[field].GetString().Should().Be(RTUB.Core.Constants.AuditConfiguration.RedactedValue, field);
        }

        changes["ConversationId"].GetInt32().Should().Be(3, "safe ids stay readable");
    }

    [Fact]
    public void CreateAuditLog_ForAConversationOrReaction_RedactsTitleParticipantsAndEmoji()
    {
        var conversation = new Conversation { Id = 3, Participants = "user-a;user-b", Title = "Grupo secreto", IsGroup = true };
        var reaction = new MessageReaction { Id = 4, MessageId = 7, UserId = "user-a", Emoji = "😂" };
        _context.Conversations.Add(conversation);
        _context.MessageReactions.Add(reaction);

        var conversationLog = _appender.CreateAuditLog(_context.Entry((BaseEntity)conversation), "Created", _testUsername, _testUserId,
            ResolveUserIdToNickname, GetEntityDisplayName);
        var reactionLog = _appender.CreateAuditLog(_context.Entry((BaseEntity)reaction), "Created", _testUsername, _testUserId,
            ResolveUserIdToNickname, GetEntityDisplayName);

        conversationLog!.Changes.Should().NotContain("Grupo secreto").And.NotContain("user-b").And.Contain("IsGroup");
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(reactionLog!.Changes!)!["Emoji"].GetString()
            .Should().Be(RTUB.Core.Constants.AuditConfiguration.RedactedValue);
    }

    [Fact]
    public void CreateAuditLog_DoesNotRedactTheSamePropertyNamesOnOtherEntities()
    {
        var album = Album.Create("Titulo visivel", 2024);
        _context.Albums.Add(album);

        var auditLog = _appender.CreateAuditLog(_context.Entry((BaseEntity)album), "Created", _testUsername, _testUserId,
            ResolveUserIdToNickname, GetEntityDisplayName);

        auditLog!.Changes.Should().Contain("Titulo visivel").And.NotContain("[redacted]");
    }
}
