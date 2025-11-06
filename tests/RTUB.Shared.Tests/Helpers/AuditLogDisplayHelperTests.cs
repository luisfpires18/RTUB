using FluentAssertions;
using RTUB.Shared.Helpers;

namespace RTUB.Shared.Tests.Helpers;

/// <summary>
/// Unit tests for AuditLogDisplayHelper
/// Tests the detection and display of ApplicationUser login events
/// </summary>
public class AuditLogDisplayHelperTests
{
    [Fact]
    public void GetDisplayInfo_NonApplicationUser_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "Event";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56.9294731Z\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_ApplicationUser_NoLastLoginDateChange_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"FirstName\": {\"Old\": \"John\", \"New\": \"Jane\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_LastLoginDate_OldNull_NewHasValue_ReturnsLoggedIn()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56.9294731Z\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Logged in");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_LastLoginDate_NewGreaterThanOld_ReturnsLoggedIn()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:55.2108508\", \"New\": \"2025-11-06T22:07:56.9294731Z\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Logged in");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_LastLoginDate_NewEqualToOld_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:56.9294731Z\", \"New\": \"2025-11-06T22:07:56.9294731Z\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_LastLoginDate_NewLessThanOld_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:56.9294731Z\", \"New\": \"2025-11-06T22:07:55.2108508\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_MixedChanges_LastLoginDateAndFirstName_ReturnsProfileUpdatedWithBadge()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56.9294731Z\"}, \"FirstName\": {\"Old\": \"John\", \"New\": \"Jane\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Profile Updated");
        result.ShowLoggedInBadge.Should().BeTrue();
    }

    [Fact]
    public void GetDisplayInfo_LastLoginDateWithMetadata_ReturnsLoggedIn()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56.9294731Z\"}, \"_TargetUser\": \"testuser@example.com\"}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Logged in");
        result.ShowLoggedInBadge.Should().BeFalse();
        result.TargetUser.Should().Be("testuser@example.com");
    }

    [Fact]
    public void GetDisplayInfo_LocalTimeWithoutZ_ParsesCorrectly()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:55.2108508\", \"New\": \"2025-11-06T22:07:56.9294731\"}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Logged in");
    }

    [Fact]
    public void GetDisplayInfo_InvalidJson_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "invalid json {";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_NullChanges_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        string? changes = null;

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_EmptyChanges_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_SecurityStampChange_WithoutLastLoginDate_ReturnsOriginalAction()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"SecurityStamp\": {\"Old\": \"old-stamp\", \"New\": \"new-stamp\"}, \"AccessFailedCount\": {\"Old\": 0, \"New\": 1}}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.DisplayAction.Should().Be("Modified");
        result.ShowLoggedInBadge.Should().BeFalse();
    }

    [Fact]
    public void GetDisplayInfo_ExtractsTargetUser_WhenPresent()
    {
        // Arrange
        var entityType = "ApplicationUser";
        var action = "Modified";
        var changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56.9294731Z\"}, \"_TargetUser\": \"admin@rtub.com\"}";

        // Act
        var result = AuditLogDisplayHelper.GetDisplayInfo(entityType, action, changes);

        // Assert
        result.TargetUser.Should().Be("admin@rtub.com");
    }

    // Debouncing tests
    private class TestLog
    {
        public string EntityType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Changes { get; set; }
        public DateTime Timestamp { get; set; }
        public string? UserName { get; set; }
    }

    [Fact]
    public void ApplyDebouncing_TwoLoginsWithin2Minutes_CollapsesToOne()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var logs = new List<TestLog>
        {
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56Z\"}}",
                Timestamp = now,
                UserName = "testuser"
            },
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:56Z\", \"New\": \"2025-11-06T22:08:30Z\"}}",
                Timestamp = now.AddSeconds(90), // 1.5 minutes later
                UserName = "testuser"
            }
        };

        // Act
        var result = AuditLogDisplayHelper.ApplyDebouncing(
            logs,
            l => l.EntityType,
            l => l.Action,
            l => l.Changes,
            l => l.Timestamp,
            l => l.UserName
        );

        // Assert
        result.Should().HaveCount(1);
        result[0].Timestamp.Should().Be(now);
    }

    [Fact]
    public void ApplyDebouncing_TwoLoginsAfter2Minutes_KeepsBoth()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var logs = new List<TestLog>
        {
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56Z\"}}",
                Timestamp = now,
                UserName = "testuser"
            },
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:56Z\", \"New\": \"2025-11-06T22:10:30Z\"}}",
                Timestamp = now.AddMinutes(3), // 3 minutes later
                UserName = "testuser"
            }
        };

        // Act
        var result = AuditLogDisplayHelper.ApplyDebouncing(
            logs,
            l => l.EntityType,
            l => l.Action,
            l => l.Changes,
            l => l.Timestamp,
            l => l.UserName
        );

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyDebouncing_DifferentUsers_KeepsBoth()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var logs = new List<TestLog>
        {
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56Z\"}}",
                Timestamp = now,
                UserName = "user1"
            },
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:08:30Z\"}}",
                Timestamp = now.AddSeconds(30),
                UserName = "user2"
            }
        };

        // Act
        var result = AuditLogDisplayHelper.ApplyDebouncing(
            logs,
            l => l.EntityType,
            l => l.Action,
            l => l.Changes,
            l => l.Timestamp,
            l => l.UserName
        );

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyDebouncing_NonLoginEvents_NotAffected()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var logs = new List<TestLog>
        {
            new TestLog
            {
                EntityType = "Event",
                Action = "Created",
                Changes = "{\"Name\": \"Concert\"}",
                Timestamp = now,
                UserName = "user1"
            },
            new TestLog
            {
                EntityType = "Event",
                Action = "Modified",
                Changes = "{\"Name\": {\"Old\": \"Concert\", \"New\": \"Big Concert\"}}",
                Timestamp = now.AddSeconds(30),
                UserName = "user1"
            }
        };

        // Act
        var result = AuditLogDisplayHelper.ApplyDebouncing(
            logs,
            l => l.EntityType,
            l => l.Action,
            l => l.Changes,
            l => l.Timestamp,
            l => l.UserName
        );

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyDebouncing_MixedChangesWithLogin_RespectsBadge()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var logs = new List<TestLog>
        {
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": null, \"New\": \"2025-11-06T22:07:56Z\"}, \"FirstName\": {\"Old\": \"John\", \"New\": \"Jane\"}}",
                Timestamp = now,
                UserName = "testuser"
            },
            new TestLog
            {
                EntityType = "ApplicationUser",
                Action = "Modified",
                Changes = "{\"LastLoginDate\": {\"Old\": \"2025-11-06T22:07:56Z\", \"New\": \"2025-11-06T22:08:30Z\"}}",
                Timestamp = now.AddSeconds(90),
                UserName = "testuser"
            }
        };

        // Act
        var result = AuditLogDisplayHelper.ApplyDebouncing(
            logs,
            l => l.EntityType,
            l => l.Action,
            l => l.Changes,
            l => l.Timestamp,
            l => l.UserName
        );

        // Assert - Second login should be debounced because both are login events
        result.Should().HaveCount(1);
    }
}
