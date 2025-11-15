using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Application.Interfaces;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the FolderCard component
/// </summary>
public class FolderCardTests : TestContext
{
    private readonly Fixture _fixture;

    public FolderCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void FolderCard_RendersFolderName()
    {
        // Arrange
        var documents = new List<DocumentMetadata>();

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "General")
            .Add(p => p.FolderPath, "docs/General")
            .Add(p => p.Documents, documents));

        // Assert
        cut.Markup.Should().Contain("General", "card should display folder name");
    }

    [Fact]
    public void FolderCard_RendersDocumentCount_Singular()
    {
        // Arrange
        var documents = new List<DocumentMetadata>
        {
            new DocumentMetadata
            {
                FileName = "test.pdf",
                FilePath = "docs/test.pdf",
                SizeBytes = 1024,
                LastModified = DateTime.UtcNow,
                Extension = ".pdf"
            }
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Test")
            .Add(p => p.FolderPath, "docs/Test")
            .Add(p => p.Documents, documents));

        // Assert
        cut.Markup.Should().Contain("(1 documento)", "card should display singular document count");
    }

    [Fact]
    public void FolderCard_RendersDocumentCount_Plural()
    {
        // Arrange
        var documents = new List<DocumentMetadata>
        {
            new DocumentMetadata
            {
                FileName = "test1.pdf",
                FilePath = "docs/test1.pdf",
                SizeBytes = 1024,
                LastModified = DateTime.UtcNow,
                Extension = ".pdf"
            },
            new DocumentMetadata
            {
                FileName = "test2.pdf",
                FilePath = "docs/test2.pdf",
                SizeBytes = 2048,
                LastModified = DateTime.UtcNow,
                Extension = ".pdf"
            }
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Test")
            .Add(p => p.FolderPath, "docs/Test")
            .Add(p => p.Documents, documents));

        // Assert
        cut.Markup.Should().Contain("(2 documentos)", "card should display plural document count");
    }

    [Fact]
    public void FolderCard_StartsCollapsed_ByDefault()
    {
        // Arrange
        var documents = new List<DocumentMetadata>();

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Test")
            .Add(p => p.FolderPath, "docs/Test")
            .Add(p => p.Documents, documents)
            .Add(p => p.DefaultExpanded, false));

        // Assert
        cut.Markup.Should().Contain("bi-folder", "collapsed folder should use folder icon");
        cut.Markup.Should().Contain("bi-chevron-down", "collapsed folder should show down chevron");
    }

    [Fact]
    public void FolderCard_CanStartExpanded()
    {
        // Arrange
        var documents = new List<DocumentMetadata>();

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Test")
            .Add(p => p.FolderPath, "docs/Test")
            .Add(p => p.Documents, documents)
            .Add(p => p.DefaultExpanded, true));

        // Assert
        cut.Markup.Should().Contain("bi-folder-open", "expanded folder should use open folder icon");
        cut.Markup.Should().Contain("bi-chevron-up", "expanded folder should show up chevron");
    }

    [Fact]
    public void FolderCard_ShowsEmptyState_WhenNoDocuments()
    {
        // Arrange
        var documents = new List<DocumentMetadata>();

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Empty")
            .Add(p => p.FolderPath, "docs/Empty")
            .Add(p => p.Documents, documents)
            .Add(p => p.DefaultExpanded, true));

        // Assert
        cut.Markup.Should().Contain("Pasta vazia", "empty folder should show empty state");
    }

    [Fact]
    public void FolderCard_ShowsDocuments_WhenExpanded()
    {
        // Arrange
        var documents = new List<DocumentMetadata>
        {
            new DocumentMetadata
            {
                FileName = "document.pdf",
                FilePath = "docs/document.pdf",
                SizeBytes = 1024,
                LastModified = DateTime.UtcNow,
                Extension = ".pdf"
            }
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Test")
            .Add(p => p.FolderPath, "docs/Test")
            .Add(p => p.Documents, documents)
            .Add(p => p.DefaultExpanded, true));

        // Assert
        cut.Markup.Should().Contain("document.pdf", "expanded folder should display documents");
        cut.Markup.Should().Contain("document-grid", "documents should be in a grid");
    }

    [Fact]
    public void FolderCard_DoesNotShowUploadButton_ForNonAdmin()
    {
        // Arrange
        var documents = new List<DocumentMetadata>();

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.FolderName, "Test")
            .Add(p => p.FolderPath, "docs/Test")
            .Add(p => p.Documents, documents)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().NotContain("Carregar", "non-admin users should not see upload button");
    }
}
