using FluentAssertions;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for AuditContext
/// Tests user context management for audit logging
/// </summary>
public class AuditContextTests
{
    [Fact]
    public void SetUser_WithValidValues_SetsUserNameAndUserId()
    {
        // Arrange
        var auditContext = new AuditContext();
        var userName = "testuser";
        var userId = "123";

        // Act
        auditContext.SetUser(userName, userId);

        // Assert
        auditContext.UserName.Should().Be(userName);
        auditContext.UserId.Should().Be(userId);
    }

    [Fact]
    public void SetUser_WithNullValues_SetsNullUserNameAndUserId()
    {
        // Arrange
        var auditContext = new AuditContext();

        // Act
        auditContext.SetUser(null, null);

        // Assert
        auditContext.UserName.Should().BeNull();
        auditContext.UserId.Should().BeNull();
    }

    [Fact]
    public void Clear_WhenCalled_SetsUserNameAndUserIdToNull()
    {
        // Arrange
        var auditContext = new AuditContext();
        auditContext.SetUser("testuser", "123");

        // Act
        auditContext.Clear();

        // Assert
        auditContext.UserName.Should().BeNull();
        auditContext.UserId.Should().BeNull();
    }

    [Fact]
    public void SetUser_CalledMultipleTimes_OverwritesPreviousValues()
    {
        // Arrange
        var auditContext = new AuditContext();
        auditContext.SetUser("user1", "1");

        // Act
        auditContext.SetUser("user2", "2");

        // Assert
        auditContext.UserName.Should().Be("user2");
        auditContext.UserId.Should().Be("2");
    }

    [Fact]
    public void InitialState_WhenCreated_HasNullValues()
    {
        // Arrange & Act
        var auditContext = new AuditContext();

        // Assert
        auditContext.UserName.Should().BeNull();
        auditContext.UserId.Should().BeNull();
    }

    [Fact]
    public void SetUser_WithEmptyStrings_SetsEmptyStringValues()
    {
        // Arrange
        var auditContext = new AuditContext();

        // Act
        auditContext.SetUser("", "");

        // Assert
        auditContext.UserName.Should().Be("");
        auditContext.UserId.Should().Be("");
    }
}
