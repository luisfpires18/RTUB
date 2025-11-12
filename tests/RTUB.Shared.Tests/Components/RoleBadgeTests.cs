using Bunit;
using FluentAssertions;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the RoleBadge component to ensure role badges display correctly
/// </summary>
public class RoleBadgeTests : TestContext
{
    [Fact]
    public void RoleBadge_RendersRole()
    {
        // Arrange
        var role = "Admin";

        // Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, role));

        // Assert
        cut.Markup.Should().Contain(role, "badge should display the role");
    }

    [Fact]
    public void RoleBadge_HasBadgeClass()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Member"));

        // Assert
        cut.Markup.Should().Contain("badge", "should have badge class");
    }

    [Fact]
    public void RoleBadge_AppliesDangerClass_ForOwnerRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Owner"));

        // Assert
        cut.Markup.Should().Contain("bg-danger", "Owner role should have danger background");
    }

    [Fact]
    public void RoleBadge_AppliesSuccessClass_ForAdminRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Admin"));

        // Assert
        cut.Markup.Should().Contain("bg-success", "Admin role should have success (green) background");
    }

    [Fact]
    public void RoleBadge_AppliesInfoClass_ForMemberRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Member"));

        // Assert
        cut.Markup.Should().Contain("bg-info", "Member role should have info (blue, not primary) background");
    }

    [Fact]
    public void RoleBadge_AppliesSecondaryClass_ForUnknownRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "UnknownRole"));

        // Assert
        cut.Markup.Should().Contain("bg-secondary", "unknown role should have secondary background");
    }

    [Fact]
    public void RoleBadge_AppliesSecondaryClass_ForEmptyRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, string.Empty));

        // Assert
        cut.Markup.Should().Contain("bg-secondary", "empty role should have secondary background");
    }

    [Fact]
    public void RoleBadge_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-class";

        // Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Admin")
            .Add(p => p.AdditionalClasses, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    [Theory]
    [InlineData("Owner", "bg-danger")]
    [InlineData("Admin", "bg-success")]
    [InlineData("Member", "bg-info")]
    public void RoleBadge_AppliesCorrectClass_ForEachRole(string role, string expectedClass)
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, role));

        // Assert
        cut.Markup.Should().Contain(expectedClass, $"role {role} should have {expectedClass}");
    }

    [Fact]
    public void RoleBadge_RendersAsSpan()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Member"));

        // Assert
        cut.Markup.Should().StartWith("<span", "badge should render as a span element");
    }

    [Fact]
    public void RoleBadge_HasAccessibilityAttributes()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Admin"));

        // Assert
        cut.Markup.Should().Contain("tabindex=\"0\"", "badge should be keyboard accessible");
        cut.Markup.Should().Contain("role=\"button\"", "badge should have button role");
        cut.Markup.Should().Contain("aria-expanded=\"false\"", "badge should have aria-expanded attribute");
    }

    [Fact]
    public void RoleBadge_HasTitleAttribute()
    {
        // Arrange
        var role = "Administrator";

        // Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, role));

        // Assert
        cut.Markup.Should().Contain($"title=\"{role}\"", "badge should have title attribute with role name");
    }

    [Fact]
    public void RoleBadge_SupportsLongRoleNames()
    {
        // Arrange
        var longRole = "Very Long Role Name That Should Be Truncated";

        // Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, longRole));

        // Assert
        cut.Markup.Should().Contain(longRole, "badge should display the full role name");
        cut.Markup.Should().Contain("title=\"", "badge should have title for long names");
    }
}
