using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class AuditLogTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var auditLog = new AuditLog();

        // Assert
        auditLog.Should().NotBeNull();
        auditLog.EntityType.Should().Be(string.Empty);
        auditLog.Action.Should().Be(string.Empty);
        auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        auditLog.IsCriticalAction.Should().BeFalse();
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            Id = 1,
            EntityType = "User",
            EntityId = 123,
            Action = "Create",
            UserId = "user-id-123",
            UserName = "John Doe",
            TargetMemberName = "Jane Doe",
            Changes = "{\"Name\": \"New Value\"}",
            EntityDisplayName = "User John",
            IsCriticalAction = true
        };

        // Assert
        auditLog.Id.Should().Be(1);
        auditLog.EntityType.Should().Be("User");
        auditLog.EntityId.Should().Be(123);
        auditLog.Action.Should().Be("Create");
        auditLog.UserId.Should().Be("user-id-123");
        auditLog.UserName.Should().Be("John Doe");
        auditLog.TargetMemberName.Should().Be("Jane Doe");
        auditLog.Changes.Should().Be("{\"Name\": \"New Value\"}");
        auditLog.EntityDisplayName.Should().Be("User John");
        auditLog.IsCriticalAction.Should().BeTrue();
    }

    [Fact]
    public void EntityId_WhenNull_ShouldAllowNullValue()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EntityType = "GlobalSetting",
            EntityId = null
        };

        // Assert
        auditLog.EntityId.Should().BeNull();
    }

    [Fact]
    public void NullableProperties_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EntityType = "Test",
            Action = "Test"
        };

        // Assert
        auditLog.UserId.Should().BeNull();
        auditLog.UserName.Should().BeNull();
        auditLog.TargetMemberName.Should().BeNull();
        auditLog.Changes.Should().BeNull();
        auditLog.EntityDisplayName.Should().BeNull();
        auditLog.EntityId.Should().BeNull();
    }

    [Fact]
    public void Timestamp_WhenSet_ShouldReturnCorrectValue()
    {
        // Arrange
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var auditLog = new AuditLog
        {
            Timestamp = specificTime
        };

        // Assert
        auditLog.Timestamp.Should().Be(specificTime);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Event")]
    [InlineData("Transaction")]
    public void EntityType_WithDifferentValues_ShouldStore(string entityType)
    {
        // Arrange
        var auditLog = new AuditLog { EntityType = entityType };

        // Assert
        auditLog.EntityType.Should().Be(entityType);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public void Action_WithDifferentValues_ShouldStore(string action)
    {
        // Arrange
        var auditLog = new AuditLog { Action = action };

        // Assert
        auditLog.Action.Should().Be(action);
    }
}
