using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Forms;

/// <summary>
/// Tests for the FormTextArea component to ensure proper rendering and value binding
/// </summary>
public class FormTextAreaTests : TestContext
{
    [Fact]
    public void FormTextArea_RendersLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Label"));

        // Assert
        cut.Markup.Should().Contain("Test Label", "should display label");
    }

    [Fact]
    public void FormTextArea_RendersIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Icon, "text-paragraph"));

        // Assert
        cut.Markup.Should().Contain("bi-text-paragraph", "should display icon");
    }

    [Fact]
    public void FormTextArea_RendersValue()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Value, "Test Value"));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.GetAttribute("value").Should().Be("Test Value", "should display value in textarea");
    }

    [Fact]
    public void FormTextArea_RendersPlaceholder()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Placeholder, "Enter description..."));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.GetAttribute("placeholder").Should().Be("Enter description...", "should display placeholder");
    }

    [Fact]
    public void FormTextArea_ShowsRequiredIndicator()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Required, true));

        // Assert
        cut.Markup.Should().Contain("text-danger", "should display required indicator");
        cut.Markup.Should().Contain("*", "should display asterisk for required");
    }

    [Fact]
    public void FormTextArea_SetsRowsAttribute()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Rows, 5));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.GetAttribute("rows").Should().Be("5", "should set rows attribute");
    }

    [Fact]
    public void FormTextArea_DefaultRowsIsThree()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field"));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.GetAttribute("rows").Should().Be("3", "default rows should be 3");
    }

    [Fact]
    public void FormTextArea_RendersHelpText()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.HelpText, "This is help text"));

        // Assert
        cut.Markup.Should().Contain("This is help text", "should display help text");
    }

    [Fact]
    public void FormTextArea_CanBeDisabled()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.Disabled, true));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.HasAttribute("disabled").Should().BeTrue("textarea should be disabled");
    }

    [Fact]
    public void FormTextArea_CanBeReadOnly()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.ReadOnly, true));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.HasAttribute("readonly").Should().BeTrue("textarea should be readonly");
    }

    [Fact]
    public void FormTextArea_HasFormControlClass()
    {
        // Arrange & Act
        var cut = RenderComponent<FormTextArea>(parameters => parameters
            .Add(p => p.Label, "Test Field"));

        // Assert
        var textarea = cut.Find("textarea");
        textarea.ClassList.Should().Contain("form-control", "should have form-control class");
    }
}
