using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Forms;

/// <summary>
/// Tests for the FormSelect component to ensure proper rendering and value binding
/// </summary>
public class FormSelectTests : TestContext
{
    [Fact]
    public void FormSelect_RendersLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Label"));

        // Assert
        cut.Markup.Should().Contain("Test Label", "should display label");
    }

    [Fact]
    public void FormSelect_RendersIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Icon, "tag"));

        // Assert
        cut.Markup.Should().Contain("bi-tag", "should display icon");
    }

    [Fact]
    public void FormSelect_RendersValue()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Value, "option1"));

        // Assert
        var select = cut.Find("select");
        select.GetAttribute("value").Should().Be("option1", "should display selected value");
    }

    [Fact]
    public void FormSelect_RendersDefaultOption()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.DefaultOption, "Select an option..."));

        // Assert
        cut.Markup.Should().Contain("Select an option...", "should display default option");
    }

    [Fact]
    public void FormSelect_RendersChildContent()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.ChildContent, (RenderFragment)((builder) =>
            {
                builder.OpenElement(0, "option");
                builder.AddAttribute(1, "value", "test");
                builder.AddContent(2, "Test Option");
                builder.CloseElement();
            })));

        // Assert
        cut.Markup.Should().Contain("Test Option", "should display child options");
    }

    [Fact]
    public void FormSelect_ShowsRequiredIndicator()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Required, true));

        // Assert
        cut.Markup.Should().Contain("text-danger", "should display required indicator");
        cut.Markup.Should().Contain("*", "should display asterisk for required");
    }

    [Fact]
    public void FormSelect_RendersHelpText()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.HelpText, "This is help text"));

        // Assert
        cut.Markup.Should().Contain("This is help text", "should display help text");
    }

    [Fact]
    public void FormSelect_CanBeDisabled()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Disabled, true));

        // Assert
        var select = cut.Find("select");
        select.HasAttribute("disabled").Should().BeTrue("select should be disabled");
    }

    [Fact]
    public void FormSelect_HasFormSelectClass()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field"));

        // Assert
        var select = cut.Find("select");
        select.ClassList.Should().Contain("form-select", "should have form-select class");
    }

    [Fact]
    public void FormSelect_AppliesAdditionalCssClasses()
    {
        // Arrange & Act
        var cut = RenderComponent<FormSelect>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.CssClass, "custom-class")
            .Add(p => p.InputCssClass, "custom-select")
            .Add(p => p.LabelCssClass, "custom-label"));

        // Assert
        cut.Markup.Should().Contain("custom-class", "should apply custom CSS class");
        cut.Markup.Should().Contain("custom-select", "should apply select CSS class");
        cut.Markup.Should().Contain("custom-label", "should apply label CSS class");
    }
}
