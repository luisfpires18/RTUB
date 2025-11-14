using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Forms;

/// <summary>
/// Tests for the FormSection component to ensure it renders correctly and groups form fields properly
/// </summary>
public class FormSectionTests : TestContext
{
    [Fact]
    public void FormSection_RendersTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.Title, "Test Section"));

        // Assert
        cut.Markup.Should().Contain("Test Section", "should display section title");
    }

    [Fact]
    public void FormSection_RendersIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.Icon, "person-fill"));

        // Assert
        cut.Markup.Should().Contain("bi-person-fill", "should display icon");
    }

    [Fact]
    public void FormSection_RendersChildContent()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.Title, "Test Section")
            .Add(p => p.ChildContent, (RenderFragment)((builder) =>
            {
                builder.AddContent(0, "Test Form Content");
            })));

        // Assert
        cut.Markup.Should().Contain("Test Form Content", "should display child content");
    }

    [Fact]
    public void FormSection_HasCardStructure()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.Title, "Test Section"));

        // Assert
        cut.Markup.Should().Contain("form-section", "should have form-section class");
        cut.Markup.Should().Contain("section-header", "should have section-header");
        cut.Markup.Should().Contain("section-body", "should have section-body");
    }

    [Fact]
    public void FormSection_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-form-section";

        // Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.Title, "Test")
            .Add(p => p.CssClass, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    [Fact]
    public void FormSection_WorksWithoutTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)((builder) =>
            {
                builder.AddContent(0, "Content without title");
            })));

        // Assert
        cut.Markup.Should().Contain("Content without title", "should display content even without title");
    }

    [Fact]
    public void FormSection_WorksWithoutIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSection>(parameters => parameters
            .Add(p => p.Title, "Test Section"));

        // Assert
        cut.Markup.Should().NotContain("bi-", "should not display icon when not provided");
        cut.Markup.Should().Contain("Test Section", "should still display title");
    }
}
