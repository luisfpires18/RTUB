using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Application.Interfaces;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the DocumentCard component
/// </summary>
public class DocumentCardTests : TestContext
{
    private readonly Fixture _fixture;

    public DocumentCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void DocumentCard_RendersFileName()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "test-document.pdf",
            FilePath = "docs/test/test-document.pdf",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".pdf"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("test-document.pdf", "card should display document filename");
    }

    [Fact]
    public void DocumentCard_RendersFileExtension()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "report.docx",
            FilePath = "docs/reports/report.docx",
            SizeBytes = 2048,
            LastModified = DateTime.UtcNow,
            Extension = ".docx"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("DOCX", "card should display file extension in uppercase");
    }

    [Fact]
    public void DocumentCard_DisplaysPdfIcon_ForPdfFiles()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "document.pdf",
            FilePath = "docs/document.pdf",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".pdf"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("bi-file-pdf", "PDF files should have PDF icon");
    }

    [Fact]
    public void DocumentCard_DisplaysWordIcon_ForWordFiles()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "document.docx",
            FilePath = "docs/document.docx",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".docx"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("bi-file-word", "Word files should have Word icon");
    }

    [Fact]
    public void DocumentCard_DisplaysExcelIcon_ForExcelFiles()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "spreadsheet.xlsx",
            FilePath = "docs/spreadsheet.xlsx",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".xlsx"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("bi-file-excel", "Excel files should have Excel icon");
    }

    [Fact]
    public void DocumentCard_FormatsFileSize_InKB()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "small.txt",
            FilePath = "docs/small.txt",
            SizeBytes = 5120, // 5 KB
            LastModified = DateTime.UtcNow,
            Extension = ".txt"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("KB", "file size should be formatted in KB");
    }

    [Fact]
    public void DocumentCard_FormatsFileSize_InMB()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "medium.pdf",
            FilePath = "docs/medium.pdf",
            SizeBytes = 5242880, // 5 MB
            LastModified = DateTime.UtcNow,
            Extension = ".pdf"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("MB", "file size should be formatted in MB");
    }

    [Fact]
    public void DocumentCard_ShowsViewButton_ForPdfFiles()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "document.pdf",
            FilePath = "docs/document.pdf",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".pdf"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("Ver", "PDF files should have a view button");
    }

    [Fact]
    public void DocumentCard_ShowsDownloadButton()
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = "document.pdf",
            FilePath = "docs/document.pdf",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".pdf"
        };

        // Act
        var cut = RenderComponent<DocumentCard>(parameters => parameters
            .Add(p => p.Document, document));

        // Assert
        cut.Markup.Should().Contain("Transferir", "all documents should have a download button");
    }
}
