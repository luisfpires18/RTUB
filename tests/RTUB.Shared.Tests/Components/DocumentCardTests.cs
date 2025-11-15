using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the DocumentCard component to ensure documents display correctly
/// </summary>
public class DocumentCardTests : TestContext
{
    [Fact]
    public void DocumentCard_RendersDocumentName()
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = "test-document.pdf",
            Path = "docs/test/test-document.pdf",
            Extension = "pdf",
            Size = 1024,
            UploadedDate = DateTime.UtcNow
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("test-document.pdf", "card should display document name");
    }

    [Theory]
    [InlineData("pdf", "bi-filetype-pdf")]
    [InlineData("docx", "bi-filetype-docx")]
    [InlineData("xlsx", "bi-filetype-xlsx")]
    [InlineData("txt", "bi-filetype-txt")]
    [InlineData("csv", "bi-filetype-csv")]
    [InlineData("unknown", "bi-file-earmark")]
    public void DocumentCard_RendersCorrectFileIcon(string extension, string expectedIcon)
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = $"test.{extension}",
            Path = $"docs/test/test.{extension}",
            Extension = extension,
            Size = 1024,
            UploadedDate = DateTime.UtcNow
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain(expectedIcon, $"card should display {expectedIcon} for {extension} files");
    }

    [Theory]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    public void DocumentCard_FormatsFileSizeCorrectly(long bytes, string expectedSize)
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = "test.pdf",
            Path = "docs/test/test.pdf",
            Extension = "pdf",
            Size = bytes,
            UploadedDate = DateTime.UtcNow
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain(expectedSize, "card should display formatted file size");
    }

    [Fact]
    public void DocumentCard_RendersViewButton()
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = "test.pdf",
            Path = "docs/test/test.pdf",
            Extension = "pdf",
            Size = 1024,
            UploadedDate = DateTime.UtcNow
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        var viewButton = cut.Find("button[title='Visualizar']");
        viewButton.Should().NotBeNull("card should have a view button");
        viewButton.TextContent.Should().Contain("Ver");
    }

    [Fact]
    public void DocumentCard_RendersDownloadButton()
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = "test.pdf",
            Path = "docs/test/test.pdf",
            Extension = "pdf",
            Size = 1024,
            UploadedDate = DateTime.UtcNow
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        var downloadButton = cut.Find("button[title='Descarregar']");
        downloadButton.Should().NotBeNull("card should have a download button");
        downloadButton.TextContent.Should().Contain("Download");
    }

    [Fact]
    public void DocumentCard_ViewButton_InvokesCallback()
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = "test.pdf",
            Path = "docs/test/test.pdf",
            Extension = "pdf",
            Size = 1024,
            UploadedDate = DateTime.UtcNow
        };

        var viewCalled = false;

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.OnView, EventCallback.Factory.Create(this, () => viewCalled = true)));

        var viewButton = cut.Find("button[title='Visualizar']");
        viewButton.Click();

        // Assert
        viewCalled.Should().BeTrue("clicking view button should invoke OnView callback");
    }

    [Fact]
    public void DocumentCard_DownloadButton_InvokesCallback()
    {
        // Arrange
        var document = new CloudflareDocument
        {
            Name = "test.pdf",
            Path = "docs/test/test.pdf",
            Extension = "pdf",
            Size = 1024,
            UploadedDate = DateTime.UtcNow
        };

        var downloadCalled = false;

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.OnDownload, EventCallback.Factory.Create(this, () => downloadCalled = true)));

        var downloadButton = cut.Find("button[title='Descarregar']");
        downloadButton.Click();

        // Assert
        downloadCalled.Should().BeTrue("clicking download button should invoke OnDownload callback");
    }

    [Fact]
    public void DocumentCard_RendersUploadDate()
    {
        // Arrange
        var uploadDate = new DateTime(2024, 1, 15);
        var document = new CloudflareDocument
        {
            Name = "test.pdf",
            Path = "docs/test/test.pdf",
            Extension = "pdf",
            Size = 1024,
            UploadedDate = uploadDate
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert - Date format is "dd MMM yyyy" which produces "15 Jan 2024"
        cut.Markup.Should().Contain("15 Jan 2024", "card should display upload date");
    }
}
