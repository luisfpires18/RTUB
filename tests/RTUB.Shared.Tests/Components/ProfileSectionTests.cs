using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ProfileSection component to ensure sections display and interact correctly
/// </summary>
public class ProfileSectionTests : TestContext
{
    [Fact]
    public void ProfileSection_RendersTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section"));

        // Assert
        cut.Markup.Should().Contain("Test Section", "should display section title");
    }

    [Fact]
    public void ProfileSection_RendersIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.Icon, "bi-person-fill"));

        // Assert
        cut.Markup.Should().Contain("bi-person-fill", "should display icon");
    }

    [Fact]
    public void ProfileSection_ShowsEditButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.ShowEditButton, true));

        // Assert
        cut.Markup.Should().Contain("Editar", "should show edit button text");
        cut.Markup.Should().Contain("bi-pencil", "should show edit icon");
    }

    [Fact]
    public void ProfileSection_HidesEditButtonWhenNotRequired()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.ShowEditButton, false));

        // Assert
        cut.Markup.Should().NotContain("Editar", "should not show edit button");
    }

    [Fact]
    public void ProfileSection_ShowsContentWhenExpanded()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsExpanded, true)
            .Add(p => p.ChildContent, (RenderFragment)((builder) =>
            {
                builder.AddContent(0, "Test Content");
            })));

        // Assert
        cut.Markup.Should().Contain("Test Content", "should display content when expanded");
    }

    [Fact]
    public void ProfileSection_ShowsChevronDownWhenCollapsed()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsExpanded, false)
            .Add(p => p.IsCollapsible, true));

        // Assert
        cut.Markup.Should().Contain("bi-chevron-down", "should show down chevron when collapsed");
    }

    [Fact]
    public void ProfileSection_ShowsChevronUpWhenExpanded()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsExpanded, true)
            .Add(p => p.IsCollapsible, true));

        // Assert
        cut.Markup.Should().Contain("bi-chevron-up", "should show up chevron when expanded");
    }

    [Fact]
    public void ProfileSection_HasCardStructure()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section"));

        // Assert
        cut.Markup.Should().Contain("card", "should have card class");
        cut.Markup.Should().Contain("profile-section", "should have profile-section class");
        cut.Markup.Should().Contain("card-header", "should have card-header");
        cut.Markup.Should().Contain("card-body", "should have card-body");
    }

    [Fact]
    public void ProfileSection_ShowsEditContentWhenEditing()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsEditing, true)
            .Add(p => p.EditContent, (RenderFragment)((builder) =>
            {
                builder.AddContent(0, "Edit Form Content");
            })));

        // Assert
        cut.Markup.Should().Contain("Edit Form Content", "should display edit content when editing");
        cut.Markup.Should().Contain("Guardar", "should show save button");
        cut.Markup.Should().Contain("Cancelar", "should show cancel button");
    }

    [Fact]
    public void ProfileSection_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-section-class";

        // Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test")
            .Add(p => p.AdditionalClasses, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    [Fact]
    public void ProfileSection_IsCollapsibleWhenConfigured()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsCollapsible, true));

        // Assert
        cut.Markup.Should().Contain("clickable", "collapsible header should be clickable");
    }

    [Fact]
    public void ProfileSection_IsNotCollapsibleWhenConfigured()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsCollapsible, false));

        // Assert
        cut.Markup.Should().NotContain("bi-chevron", "non-collapsible section should not show chevron");
    }

    [Fact]
    public void ProfileSection_ShowsCollapsedClassWhenNotExpanded()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.IsExpanded, false));

        // Assert
        cut.Markup.Should().Contain("collapsed", "should have collapsed class when not expanded");
    }
}
