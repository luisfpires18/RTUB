using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the DetailsModal component to ensure details modal functionality works correctly
/// </summary>
public class DetailsModalTests : TestContext
{
    [Fact]
    public void DetailsModal_WhenShowIsFalse_DoesNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, false));

        // Assert
        cut.Markup.Should().BeEmpty("modal should not render when Show is false");
    }

    [Fact]
    public void DetailsModal_WhenShowIsTrue_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.HeaderTitle, "Test Header"));

        // Assert
        cut.Markup.Should().NotBeEmpty("modal should render when Show is true");
        cut.Markup.Should().Contain("modal", "modal should render as a modal element");
    }

    [Fact]
    public void DetailsModal_RendersTitle()
    {
        // Arrange
        var title = "Details Modal Title";

        // Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, title)
            .Add(p => p.HeaderTitle, "Header"));

        // Assert
        cut.Markup.Should().Contain(title, "title should be displayed");
    }

    [Fact]
    public void DetailsModal_RendersHeaderTitle()
    {
        // Arrange
        var headerTitle = "Item Name";

        // Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Modal")
            .Add(p => p.HeaderTitle, headerTitle));

        // Assert
        cut.Markup.Should().Contain(headerTitle, "header title should be displayed");
    }

    [Fact]
    public void DetailsModal_RendersIconWhenProvided()
    {
        // Arrange
        var iconClass = "bi-music-note-beamed";

        // Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Modal")
            .Add(p => p.HeaderTitle, "Header")
            .Add(p => p.IconClass, iconClass));

        // Assert
        cut.Markup.Should().Contain(iconClass, "icon class should be in the markup");
    }

    [Fact]
    public void DetailsModal_RendersImageUrlWhenProvided()
    {
        // Arrange
        var imageUrl = "/images/test.jpg";

        // Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Modal")
            .Add(p => p.HeaderTitle, "Header")
            .Add(p => p.ImageUrl, imageUrl));

        // Assert
        cut.Markup.Should().Contain(imageUrl, "image URL should be in the markup");
    }

    [Fact]
    public void DetailsModal_IsCentered()
    {
        // Arrange & Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Modal")
            .Add(p => p.HeaderTitle, "Header"));

        // Assert
        cut.Markup.Should().Contain("modal-dialog-centered", "modal should be centered");
    }

    [Fact]
    public void DetailsModal_IsLargeSize()
    {
        // Arrange & Act
        var cut = RenderComponent<DetailsModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Modal")
            .Add(p => p.HeaderTitle, "Header"));

        // Assert
        cut.Markup.Should().Contain("modal-lg", "modal should be large size");
    }
}
