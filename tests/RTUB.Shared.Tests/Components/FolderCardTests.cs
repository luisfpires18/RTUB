using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the FolderCard component to ensure folders display correctly
/// </summary>
public class FolderCardTests : TestContext
{
    [Fact]
    public void FolderCard_RendersFolderName()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 5
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("Test Folder", "card should display folder name");
    }

    [Fact]
    public void FolderCard_RendersFileCount()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 3
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("3", "card should display file count");
        cut.Markup.Should().Contain("ficheiros", "card should display 'ficheiros' for multiple files");
    }

    [Fact]
    public void FolderCard_RendersSingularFileCount()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("1", "card should display file count");
        cut.Markup.Should().Contain("ficheiro", "card should display 'ficheiro' for single file");
    }

    [Fact]
    public void FolderCard_StartsCollapsed_ByDefault()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 3
        };

        var documents = new List<CloudflareDocument>
        {
            new CloudflareDocument { Name = "doc1.pdf", Path = "docs/test/doc1.pdf", Extension = "pdf", Size = 1000, UploadedDate = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, false)
            .Add(p => p.Documents, documents));

        // Assert
        cut.FindAll(".folder-card-body").Should().BeEmpty("folder should start collapsed");
    }

    [Fact]
    public void FolderCard_StartsExpanded_WhenSpecified()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        var documents = new List<CloudflareDocument>
        {
            new CloudflareDocument { Name = "doc1.pdf", Path = "docs/test/doc1.pdf", Extension = "pdf", Size = 1000, UploadedDate = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, true)
            .Add(p => p.Documents, documents));

        // Assert
        cut.FindAll(".folder-card-body").Should().NotBeEmpty("folder should start expanded");
    }

    [Fact]
    public void FolderCard_TogglesExpansion_OnHeaderClick()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        var documents = new List<CloudflareDocument>
        {
            new CloudflareDocument { Name = "doc1.pdf", Path = "docs/test/doc1.pdf", Extension = "pdf", Size = 1000, UploadedDate = DateTime.UtcNow }
        };

        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, false)
            .Add(p => p.Documents, documents));

        // Act - Click header to expand
        var header = cut.Find(".folder-card-header");
        header.Click();

        // Assert
        cut.FindAll(".folder-card-body").Should().NotBeEmpty("folder should expand after clicking header");

        // Act - Click header again to collapse
        header.Click();

        // Assert
        cut.FindAll(".folder-card-body").Should().BeEmpty("folder should collapse after clicking header again");
    }

    [Fact]
    public void FolderCard_RendersDocumentCards_WhenExpanded()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 2
        };

        var documents = new List<CloudflareDocument>
        {
            new CloudflareDocument { Name = "doc1.pdf", Path = "docs/test/doc1.pdf", Extension = "pdf", Size = 1000, UploadedDate = DateTime.UtcNow },
            new CloudflareDocument { Name = "doc2.docx", Path = "docs/test/doc2.docx", Extension = "docx", Size = 2000, UploadedDate = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, true)
            .Add(p => p.Documents, documents));

        // Assert
        cut.Markup.Should().Contain("doc1.pdf", "should render first document");
        cut.Markup.Should().Contain("doc2.docx", "should render second document");
    }

    [Fact]
    public void FolderCard_ShowsLoadingSpinner_WhenLoading()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, true)
            .Add(p => p.IsLoading, true)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("spinner-border", "should show loading spinner");
        cut.Markup.Should().Contain("A carregar...", "should show loading message");
    }

    [Fact]
    public void FolderCard_ShowsEmptyMessage_WhenNoDocuments()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 0
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, true)
            .Add(p => p.IsLoading, false)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("Nenhum documento nesta pasta", "should show empty message");
    }

    [Fact]
    public void FolderCard_InvokesExpandedChangedCallback()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        var expandedState = false;

        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, false)
            .Add(p => p.Documents, new List<CloudflareDocument>())
            .Add(p => p.OnExpandedChanged, EventCallback.Factory.Create<bool>(this, (state) => expandedState = state)));

        // Act
        var header = cut.Find(".folder-card-header");
        header.Click();

        // Assert
        expandedState.Should().BeTrue("OnExpandedChanged should be invoked with true");
    }

    [Fact]
    public void FolderCard_ShowsCorrectIcon_WhenCollapsed()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, false)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("bi-folder2", "should show closed folder icon when collapsed");
        cut.Markup.Should().Contain("bi-chevron-down", "should show chevron down when collapsed");
    }

    [Fact]
    public void FolderCard_ShowsCorrectIcon_WhenExpanded()
    {
        // Arrange
        var folder = new CloudflareFolder
        {
            Name = "Test Folder",
            Path = "docs/test-folder/",
            FileCount = 1
        };

        // Act
        var cut = RenderComponent<FolderCard>(parameters => parameters
            .Add(p => p.Folder, folder)
            .Add(p => p.StartExpanded, true)
            .Add(p => p.Documents, new List<CloudflareDocument>()));

        // Assert
        cut.Markup.Should().Contain("bi-folder2-open", "should show open folder icon when expanded");
        cut.Markup.Should().Contain("bi-chevron-up", "should show chevron up when expanded");
    }
}
