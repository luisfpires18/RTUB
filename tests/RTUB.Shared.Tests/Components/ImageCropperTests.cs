using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ImageCropper component to ensure image cropping functionality works correctly
/// </summary>
public class ImageCropperTests : TestContext
{
    [Fact]
    public void ImageCropper_DoesNotRender_WhenShowModalIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, false));

        // Assert
        cut.Markup.Should().BeEmpty("image cropper should not render when ShowModal is false");
    }

    [Fact]
    public void ImageCropper_RendersModal_WhenShowModalIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("modal", "image cropper should render as a modal");
    }

    [Fact]
    public void ImageCropper_HasCropImageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("Crop Image", "modal should have 'Crop Image' title");
    }

    [Fact]
    public void ImageCropper_HasRotateButtons()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("bi-arrow-counterclockwise", "should have rotate left button");
        cut.Markup.Should().Contain("bi-arrow-clockwise", "should have rotate right button");
    }

    [Fact]
    public void ImageCropper_HasZoomButtons()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("bi-zoom-in", "should have zoom in button");
        cut.Markup.Should().Contain("bi-zoom-out", "should have zoom out button");
    }

    [Fact]
    public void ImageCropper_HasResetButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("Reset", "should have reset button");
    }

    [Fact]
    public void ImageCropper_HasCropAndSaveButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("Crop & Save", "should have crop and save button");
    }

    [Fact]
    public void ImageCropper_HasCancelButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("Cancel", "should have cancel button");
    }

    [Fact]
    public void ImageCropper_DisplaysAspectRatioHelp_WhenProvided()
    {
        // Arrange
        var helpText = "Use 1:1 aspect ratio for profile pictures";

        // Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true)
            .Add(p => p.AspectRatioHelp, helpText));

        // Assert
        cut.Markup.Should().Contain(helpText, "should display aspect ratio help text");
    }

    [Fact]
    public void ImageCropper_DoesNotDisplayAspectRatioHelp_WhenNotProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true)
            .Add(p => p.AspectRatioHelp, string.Empty));

        // Assert
        cut.Markup.Should().NotContain("text-muted", "should not display help text section when not provided");
    }

    [Fact]
    public void ImageCropper_UsesLargeModalSize()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("modal-lg", "image cropper should use large modal size");
    }

    [Fact]
    public void ImageCropper_IsCentered()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("modal-dialog-centered", "image cropper should be centered");
    }

    [Fact]
    public void ImageCropper_HasImageElement()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("cropper-image", "should have image element with cropper-image class");
    }

    [Fact]
    public void ImageCropper_HasPrimaryPurpleButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageCropper>(parameters => parameters
            .Add(p => p.ShowModal, true));

        // Assert
        cut.Markup.Should().Contain("btn-primary-purple", "crop & save button should have primary purple style");
    }
}
