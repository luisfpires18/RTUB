using Bunit;
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ProfileField component to ensure fields display correctly
/// </summary>
public class ProfileFieldTests : TestContext
{
    [Fact]
    public void ProfileField_RendersLabelAndValue()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Test Label")
            .Add(p => p.Value, "Test Value"));

        // Assert
        cut.Markup.Should().Contain("Test Label:", "label should be displayed");
        cut.Markup.Should().Contain("Test Value", "value should be displayed");
    }

    [Fact]
    public void ProfileField_ShowsNaoDefinidoForEmptyValue()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Empty Field")
            .Add(p => p.Value, (string?)null));

        // Assert
        cut.Markup.Should().Contain("Não definido", "should show 'Não definido' for null value");
        cut.Markup.Should().Contain("bi-info-circle-fill", "should show info icon");
    }

    [Fact]
    public void ProfileField_ShowsNaoDefinidoForWhitespaceValue()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Empty Field")
            .Add(p => p.Value, "   "));

        // Assert
        cut.Markup.Should().Contain("Não definido", "should show 'Não definido' for whitespace value");
    }

    [Fact]
    public void ProfileField_HasResponsiveClasses()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Value, "Value"));

        // Assert
        cut.Markup.Should().Contain("col-12 col-md-4", "should have responsive column classes for label");
        cut.Markup.Should().Contain("col-12 col-md-8", "should have responsive column classes for value");
    }

    [Fact]
    public void ProfileField_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-class";

        // Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Value, "Value")
            .Add(p => p.AdditionalClasses, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    [Fact]
    public void ProfileField_ShowsTooltipOnEmptyValue()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Empty")
            .Add(p => p.Value, (string?)null)
            .Add(p => p.ShowTooltip, true));

        // Assert
        cut.Markup.Should().Contain("title=", "should have title attribute for tooltip");
        cut.Markup.Should().Contain("Este campo ainda não foi preenchido", "should have tooltip text");
    }

    [Fact]
    public void ProfileField_HidesTooltipWhenShowTooltipIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<ProfileField>(parameters => parameters
            .Add(p => p.Label, "Empty")
            .Add(p => p.Value, (string?)null)
            .Add(p => p.ShowTooltip, false));

        // Assert
        cut.Markup.Should().NotContain("bi-info-circle-fill", "should not show info icon when ShowTooltip is false");
    }
}
