using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Forms;

/// <summary>
/// Tests for the FormTextField component to ensure proper rendering and value binding
/// </summary>
public class FormTextFieldTests : TestContext
{
    [Fact]
    public void FormTextField_RendersLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Label"));

        // Assert
        cut.Markup.Should().Contain("Test Label", "should display label");
    }

    [Fact]
    public void FormTextField_RendersIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Icon, "person"));

        // Assert
        cut.Markup.Should().Contain("bi-person", "should display icon");
    }

    [Fact]
    public void FormTextField_RendersValue()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Value, "Test Value"));

        // Assert
        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be("Test Value", "should display value in input");
    }

    [Fact]
    public void FormTextField_RendersPlaceholder()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Placeholder, "Enter text..."));

        // Assert
        var input = cut.Find("input");
        input.GetAttribute("placeholder").Should().Be("Enter text...", "should display placeholder");
    }

    [Fact]
    public void FormTextField_ShowsRequiredIndicator()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Required, true));

        // Assert
        cut.Markup.Should().Contain("text-danger", "should display required indicator");
        cut.Markup.Should().Contain("*", "should display asterisk for required");
    }

    [Fact]
    public void FormTextField_RendersHelpText()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.HelpText, "This is help text"));

        // Assert
        cut.Markup.Should().Contain("This is help text", "should display help text");
    }

    [Fact]
    public void FormTextField_CanBeDisabled()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Disabled, true));

        // Assert
        var input = cut.Find("input");
        input.HasAttribute("disabled").Should().BeTrue("input should be disabled");
    }

    [Fact]
    public void FormTextField_CanBeReadOnly()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.ReadOnly, true));

        // Assert
        var input = cut.Find("input");
        input.HasAttribute("readonly").Should().BeTrue("input should be readonly");
    }

    [Fact]
    public void FormTextField_AppliesAdditionalCssClasses()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.CssClass, "custom-class")
            .Add(p => p.InputCssClass, "custom-input")
            .Add(p => p.LabelCssClass, "custom-label"));

        // Assert
        cut.Markup.Should().Contain("custom-class", "should apply custom CSS class");
        cut.Markup.Should().Contain("custom-input", "should apply input CSS class");
        cut.Markup.Should().Contain("custom-label", "should apply label CSS class");
    }

    [Fact]
    public void FormTextField_HasFormControlClass()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextField>(parameters => parameters
            .Add(p => p.Label, "Test Field"));

        // Assert
        var input = cut.Find("input");
        input.ClassList.Should().Contain("form-control", "should have form-control class");
    }
}
