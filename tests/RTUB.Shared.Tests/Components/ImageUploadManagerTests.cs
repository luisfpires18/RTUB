using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ImageUploadManager component to ensure file upload functionality works correctly
/// </summary>
public class ImageUploadManagerTests : TestContext
{
    [Fact]
    public void ImageUploadManager_RendersLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.Label, "Upload Photo"));

        // Assert
        cut.Markup.Should().Contain("Upload Photo", "label should be displayed");
    }

    [Fact]
    public void ImageUploadManager_RendersDefaultLabel_WhenNoLabelProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>();

        // Assert
        cut.Markup.Should().Contain("Imagem", "default label should be displayed");
    }

    [Fact]
    public void ImageUploadManager_RendersFileInput()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>();

        // Assert
        cut.Markup.Should().Contain("input", "file input should be rendered");
        cut.Markup.Should().Contain("type=\"file\"", "input should be of type file");
        cut.Markup.Should().Contain("accept=\"image/*\"", "input should accept only images");
    }

    [Fact]
    public void ImageUploadManager_ShowsCurrentImage_WhenUrlProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.CurrentImageUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentImage, true));

        // Assert
        cut.Markup.Should().Contain("Imagem atual", "should show current image label");
        cut.Markup.Should().Contain("https://example.com/image.jpg", "should show current image URL");
        cut.Markup.Should().Contain("img-thumbnail", "image should have thumbnail class");
    }

    [Fact]
    public void ImageUploadManager_DoesNotShowCurrentImage_WhenShowCurrentImageIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.CurrentImageUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentImage, false));

        // Assert
        cut.Markup.Should().NotContain("Imagem atual", "should not show current image section");
    }

    [Fact]
    public void ImageUploadManager_DoesNotShowCurrentImage_WhenNoUrlProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.ShowCurrentImage, true));

        // Assert
        cut.Markup.Should().NotContain("Imagem atual", "should not show current image section without URL");
    }

    [Fact]
    public void ImageUploadManager_AppliesCustomPreviewCssClass()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.CurrentImageUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentImage, true)
            .Add(p => p.PreviewCssClass, "custom-preview-class"));

        // Assert
        cut.Markup.Should().Contain("custom-preview-class", "should apply custom CSS class to preview");
    }

    [Fact]
    public void ImageUploadManager_AppliesDefaultPreviewCssClass_WhenNotProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.CurrentImageUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentImage, true));

        // Assert
        cut.Markup.Should().Contain("music-album-preview", "should apply default CSS class");
    }

    [Fact]
    public void ImageUploadManager_HasFormControl()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>();

        // Assert
        cut.Markup.Should().Contain("form-control", "should have form control class");
        cut.Markup.Should().Contain("file-input-dark", "should have dark file input class");
    }

    [Fact]
    public void ImageUploadManager_HasFormLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>();

        // Assert
        cut.Markup.Should().Contain("form-label", "should have form label class");
    }

    [Theory]
    [InlineData("Profile Picture")]
    [InlineData("Album Cover")]
    [InlineData("Event Banner")]
    public void ImageUploadManager_RendersCustomLabels(string customLabel)
    {
        // Arrange & Act
        var cut = RenderComponent<ImageUploadManager>(parameters => parameters
            .Add(p => p.Label, customLabel));

        // Assert
        cut.Markup.Should().Contain(customLabel, $"should display custom label: {customLabel}");
    }
}
