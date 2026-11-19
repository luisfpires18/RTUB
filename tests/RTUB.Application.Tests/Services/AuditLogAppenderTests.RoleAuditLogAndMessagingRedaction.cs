using System.Text.Json;
using FluentAssertions;

namespace RTUB.Application.Tests.Services;

// AuditLogAppenderTests: Role Audit Log And Messaging Redaction tests.
public partial class AuditLogAppenderTests
{
    [Fact]
    public void CreateRoleAuditLog_WhenRoleAdded_ReturnsAuditLog()
    {
        // Act
        var auditLog = _appender.CreateRoleAuditLog(
            "Role Added",
            "target-user-id",
            "role-id",
            "targetuser",
            "Admin",
            _testUsername,
            _testUserId);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog.EntityType.Should().Be("UserRole");
        auditLog.Action.Should().Be("Role Added");
        auditLog.IsCriticalAction.Should().BeTrue(); // Role changes are always critical
        auditLog.Changes.Should().NotBeNull();

        var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Changes!);
        changes.Should().ContainKey("Username");
        changes.Should().ContainKey("Role");
    }

    [Fact]
    public void CreateRoleAuditLog_WhenRoleRemoved_ReturnsAuditLog()
    {
        // Act
        var auditLog = _appender.CreateRoleAuditLog(
            "Role Removed",
            "target-user-id",
            "role-id",
            "targetuser",
            "Admin",
            _testUsername,
            _testUserId);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog.Action.Should().Be("Role Removed");
        auditLog.IsCriticalAction.Should().BeTrue();
    }

    // ---------- Messaging v2 (task 041): private conversations never reach /owner/tracing ----------

    [Fact]
    public void MessagingEntities_AreExcludedFromTheAuditLog()
    {
        RTUB.Core.Constants.AuditConfiguration.ExcludedEntityTypes.Should()
            .Contain(new[] { "Message", "Conversation", "MessageReaction", "ConversationUserSettings" });
    }
}
