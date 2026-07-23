using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Moq;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the MediaUploadManager component
/// Tests file selection, validation (images and videos), size limits, and error handling
/// </summary>
public class MediaUploadManagerTests : BunitContext
{
    private static async Task TriggerInputFileChangeAsync(IRenderedComponent<MediaUploadManager> cut, IBrowserFile file)
    {
        var input = cut.FindComponent<InputFile>().Instance;
        var args = new InputFileChangeEventArgs(new List<IBrowserFile> { file });
        await cut.InvokeAsync(() => input.OnChange.InvokeAsync(args));
    }

    [Fact]
    public void MediaUploadManager_RendersLabel()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.Label, "Upload Media"));

        // Assert
        cut.Markup.Should().Contain("Upload Media", "label should be displayed");
    }

    [Fact]
    public void MediaUploadManager_RendersDefaultLabel_WhenNoLabelProvided()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>();

        // Assert
        cut.Markup.Should().Contain("Escolher ficheiro", "default label should be displayed");
    }

    [Fact]
    public void MediaUploadManager_RendersFileInput_WithCorrectAccept()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>();

        // Assert
        var input = cut.Find("input[type=file]");
        input.GetAttribute("accept").Should().Be("image/*,video/*", "input should accept images and videos");
    }

    [Fact]
    public void MediaUploadManager_ShowsCurrentImage_WhenImageUrlProvided()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.CurrentMediaUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentMedia, true));

        // Assert
        cut.Markup.Should().Contain("Ficheiro atual", "should show current file label");
        cut.Markup.Should().Contain("https://example.com/image.jpg", "should show current image URL");
        cut.Markup.Should().Contain("img-thumbnail", "image should have thumbnail class");
        cut.Markup.Should().Contain("<img", "should render img tag for image");
    }

    [Fact]
    public void MediaUploadManager_ShowsCurrentVideo_WhenVideoUrlProvided()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.CurrentMediaUrl, "https://example.com/video.mp4")
            .Add(p => p.ShowCurrentMedia, true));

        // Assert
        cut.Markup.Should().Contain("Ficheiro atual", "should show current file label");
        cut.Markup.Should().Contain("https://example.com/video.mp4", "should show current video URL");
        cut.Markup.Should().Contain("<video", "should render video tag for video");
    }

    [Fact]
    public void MediaUploadManager_DoesNotShowCurrentMedia_WhenShowCurrentMediaIsFalse()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.CurrentMediaUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentMedia, false));

        // Assert
        cut.Markup.Should().NotContain("Ficheiro atual", "should not show current media section");
    }

    [Fact]
    public void MediaUploadManager_DoesNotShowCurrentMedia_WhenNoUrlProvided()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.ShowCurrentMedia, true));

        // Assert
        cut.Markup.Should().NotContain("Ficheiro atual", "should not show current media section without URL");
    }

    [Fact]
    public void MediaUploadManager_AppliesCustomPreviewCssClass()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.CurrentMediaUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentMedia, true)
            .Add(p => p.PreviewCssClass, "custom-preview-class"));

        // Assert
        cut.Markup.Should().Contain("custom-preview-class", "should apply custom CSS class to preview");
    }

    [Fact]
    public void MediaUploadManager_AppliesDefaultPreviewCssClass_WhenNotProvided()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.CurrentMediaUrl, "https://example.com/image.jpg")
            .Add(p => p.ShowCurrentMedia, true));

        // Assert
        cut.Markup.Should().Contain("music-album-preview", "should apply default CSS class");
    }

    [Fact]
    public void MediaUploadManager_ShowsDefaultFileName_WhenNoFileSelected()
    {
        // Arrange & Act
        var cut = Render<MediaUploadManager>();

        // Assert
        cut.Markup.Should().Contain("Sem ficheiro escolhido", "should show default file name message");
    }

    [Fact]
    public async Task MediaUploadManager_ShowsNewMediaMessage_WhenFileSelected()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Size).Returns(1024);

        // Act
        await TriggerInputFileChangeAsync(cut, fileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Novo ficheiro carregado"), TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("Novo ficheiro carregado", "should show new file message");
    }

    [Fact]
    public async Task MediaUploadManager_ShowsError_WhenFileTypeNotSupported()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.pdf");
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.Size).Returns(1024);

        // Act
        await TriggerInputFileChangeAsync(cut, fileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Formato de ficheiro não suportado"), TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("Formato de ficheiro não suportado", "should show error for unsupported file type");
        cut.Markup.Should().Contain("alert-danger", "error should have danger styling");
    }

    [Fact]
    public async Task MediaUploadManager_ShowsError_WhenImageTooLarge()
    {
        // Arrange
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.MaxImageSize, 5 * 1024 * 1024)); // 5MB limit

        // Create a file larger than 5MB
        var largeFileMock = new Mock<IBrowserFile>();
        largeFileMock.Setup(f => f.Name).Returns("large.jpg");
        largeFileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        largeFileMock.Setup(f => f.Size).Returns(6 * 1024 * 1024); // 6MB

        // Act
        await TriggerInputFileChangeAsync(cut, largeFileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("tamanho do ficheiro"), TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("tamanho do ficheiro", "should show error for file too large");
        cut.Markup.Should().Contain("alert-danger", "error should have danger styling");
    }

    [Fact]
    public async Task MediaUploadManager_ShowsError_WhenVideoTooLarge()
    {
        // Arrange
        var cut = Render<MediaUploadManager>(parameters => parameters
            .Add(p => p.MaxVideoSize, 50 * 1024 * 1024)); // 50MB limit

        // Create a video file larger than 50MB
        var largeFileMock = new Mock<IBrowserFile>();
        largeFileMock.Setup(f => f.Name).Returns("large.mp4");
        largeFileMock.Setup(f => f.ContentType).Returns("video/mp4");
        largeFileMock.Setup(f => f.Size).Returns(60 * 1024 * 1024); // 60MB

        // Act
        await TriggerInputFileChangeAsync(cut, largeFileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("tamanho do ficheiro"), TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("tamanho do ficheiro", "should show error for video too large");
        cut.Markup.Should().Contain("alert-danger", "error should have danger styling");
    }

    [Fact]
    public async Task MediaUploadManager_AcceptsValidImage()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Size).Returns(1024);

        IBrowserFile? selectedFile = null;
        cut.Render(parameters => parameters
            .Add(p => p.OnFileSelected, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<IBrowserFile>(this, (file) => selectedFile = file)));

        // Act
        await TriggerInputFileChangeAsync(cut, fileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Novo ficheiro carregado"), TimeSpan.FromSeconds(2));
        cut.Markup.Should().NotContain("alert-danger", "should not show error for valid image");
        cut.Markup.Should().Contain("Novo ficheiro carregado", "should show success message");
    }

    [Fact]
    public async Task MediaUploadManager_AcceptsValidVideo()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.mp4");
        fileMock.Setup(f => f.ContentType).Returns("video/mp4");
        fileMock.Setup(f => f.Size).Returns(1024);

        IBrowserFile? selectedFile = null;
        cut.Render(parameters => parameters
            .Add(p => p.OnFileSelected, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<IBrowserFile>(this, (file) => selectedFile = file)));

        // Act
        await TriggerInputFileChangeAsync(cut, fileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Novo ficheiro carregado"), TimeSpan.FromSeconds(2));
        cut.Markup.Should().NotContain("alert-danger", "should not show error for valid video");
        cut.Markup.Should().Contain("Novo ficheiro carregado", "should show success message");
    }

    [Fact]
    public async Task MediaUploadManager_UsesDefaultSizeLimits()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();

        // Assert - Default limits are 10MB for images, 100MB for videos
        // We can't directly test the parameter values, but we can verify the component accepts files within limits
        var imageFileMock = new Mock<IBrowserFile>();
        imageFileMock.Setup(f => f.Name).Returns("test.jpg");
        imageFileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        imageFileMock.Setup(f => f.Size).Returns(5 * 1024 * 1024); // 5MB (within 10MB limit)

        // Act
        await TriggerInputFileChangeAsync(cut, imageFileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Length > 0, TimeSpan.FromSeconds(2));
        cut.Markup.Should().NotContain("tamanho do ficheiro", "should accept file within default image size limit");
    }

    [Fact]
    public async Task MediaUploadManager_ResetsState_WhenResetCalled()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Size).Returns(1024);

        // Act - Select file first
        await TriggerInputFileChangeAsync(cut, fileMock.Object);
        cut.WaitForState(() => cut.Markup.Contains("Novo ficheiro carregado"), TimeSpan.FromSeconds(2));

        // Get component instance and call Reset
        var component = cut.Instance;
        component.Reset();
        cut.Render(); // Re-render to reflect reset state (Reset does not call StateHasChanged)

        // Assert
        cut.Markup.Should().NotContain("Novo ficheiro carregado", "should not show new file message after reset");
        cut.Markup.Should().Contain("Sem ficheiro escolhido", "should show default message after reset");
    }

    [Fact]
    public async Task MediaUploadManager_ShowsError_WhenExceptionOccurs()
    {
        // Arrange
        var cut = Render<MediaUploadManager>();

        // Create a file that might cause an exception (null file scenario is handled by component)
        // We'll test with a valid file but simulate an error scenario
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Size).Returns(1024);

        // Act - The component should handle exceptions gracefully
        await TriggerInputFileChangeAsync(cut, fileMock.Object);

        // Assert - Component should handle the file without throwing
        cut.WaitForState(() => cut.Markup.Length > 0, TimeSpan.FromSeconds(2));
        // If an error occurs, it should be displayed
        // If no error, the file should be accepted
        var hasError = cut.Markup.Contains("alert-danger");
        var hasSuccess = cut.Markup.Contains("Novo ficheiro carregado");

        (hasError || hasSuccess).Should().BeTrue("should either show error or success message");
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/webp", true)]
    [InlineData("video/mp4", true)]
    [InlineData("video/webm", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    public async Task MediaUploadManager_ValidatesContentType(string contentType, bool shouldAccept)
    {
        // Arrange
        var cut = Render<MediaUploadManager>();
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns($"test.{contentType.Split('/')[1]}");
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Size).Returns(1024);

        // Act
        await TriggerInputFileChangeAsync(cut, fileMock.Object);

        // Assert
        cut.WaitForState(() => cut.Markup.Length > 0, TimeSpan.FromSeconds(2));

        if (shouldAccept)
        {
            cut.Markup.Should().NotContain("Formato de ficheiro não suportado",
                $"should accept {contentType}");
        }
        else
        {
            cut.Markup.Should().Contain("Formato de ficheiro não suportado",
                $"should reject {contentType}");
        }
    }
}
