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
    public void RoleBadge_AppliesWarningClass_ForAdminRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Admin"));

        // Assert
        cut.Markup.Should().Contain("bg-warning", "Admin role should have warning background");
        cut.Markup.Should().Contain("text-dark", "Admin role should have dark text");
    }

    [Fact]
    public void RoleBadge_AppliesPrimaryClass_ForMemberRole()
    {
        // Arrange & Act
        var cut = RenderComponent<RoleBadge>(parameters => parameters
            .Add(p => p.Role, "Member"));

        // Assert
        cut.Markup.Should().Contain("bg-primary", "Member role should have primary background");
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
    [InlineData("Admin", "bg-warning")]
    [InlineData("Member", "bg-primary")]
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
}
