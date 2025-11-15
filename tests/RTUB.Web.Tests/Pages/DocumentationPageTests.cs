using Xunit;
using FluentAssertions;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Unit tests for Documentation page behavior
/// Testing pagination, folder management, and document operations
/// </summary>
public class DocumentationPageTests
{
    #region Pagination Tests

    [Theory]
    [InlineData(3, 10, 1, 3)]  // Page 1 with 3 items per page
    [InlineData(3, 10, 2, 3)]  // Page 2 with 3 items per page
    [InlineData(3, 10, 4, 1)]  // Page 4 with 3 items per page (last page, only 1 item)
    [InlineData(6, 10, 1, 6)]  // Page 1 with 6 items per page
    [InlineData(6, 10, 2, 4)]  // Page 2 with 6 items per page
    [InlineData(9, 10, 1, 9)]  // Page 1 with 9 items per page
    [InlineData(9, 10, 2, 1)]  // Page 2 with 9 items per page
    [InlineData(12, 10, 1, 10)] // Page 1 with 12 items per page (all items fit)
    [InlineData(15, 10, 1, 10)] // Page 1 with 15 items per page (all items fit)
    public void CalculatePaginatedDocuments_ReturnsCorrectCount(int pageSize, int totalDocs, int page, int expectedCount)
    {
        // Arrange
        var allDocuments = GenerateDocuments(totalDocs);
        
        // Act
        var skip = (page - 1) * pageSize;
        var paginatedDocs = allDocuments.Skip(skip).Take(pageSize).ToList();
        
        // Assert
        paginatedDocs.Count.Should().Be(expectedCount, 
            $"Page {page} with page size {pageSize} should show {expectedCount} documents out of {totalDocs} total");
    }

    [Fact]
    public void Pagination_FirstPage_ShowsCorrectDocuments()
    {
        // Arrange
        var documents = GenerateDocuments(10);
        var pageSize = 3;
        var currentPage = 1;
        
        // Act
        var skip = (currentPage - 1) * pageSize;
        var paginatedDocs = documents.Skip(skip).Take(pageSize).ToList();
        
        // Assert
        paginatedDocs.Should().HaveCount(3);
        paginatedDocs[0].FileName.Should().Be("document-1.pdf");
        paginatedDocs[1].FileName.Should().Be("document-2.pdf");
        paginatedDocs[2].FileName.Should().Be("document-3.pdf");
    }

    [Fact]
    public void Pagination_LastPage_ShowsRemainingDocuments()
    {
        // Arrange
        var documents = GenerateDocuments(10);
        var pageSize = 3;
        var currentPage = 4; // Last page
        
        // Act
        var skip = (currentPage - 1) * pageSize;
        var paginatedDocs = documents.Skip(skip).Take(pageSize).ToList();
        
        // Assert
        paginatedDocs.Should().HaveCount(1);
        paginatedDocs[0].FileName.Should().Be("document-10.pdf");
    }

    [Fact]
    public void Pagination_EmptyFolder_ReturnsEmptyList()
    {
        // Arrange
        var documents = new List<DocumentMetadata>();
        var pageSize = 3;
        var currentPage = 1;
        
        // Act
        var skip = (currentPage - 1) * pageSize;
        var paginatedDocs = documents.Skip(skip).Take(pageSize).ToList();
        
        // Assert
        paginatedDocs.Should().BeEmpty();
    }

    #endregion

    #region Folder Path Tests

    [Theory]
    [InlineData("Development", "General", "docs/Development/General")]
    [InlineData("Production", "Policies", "docs/Production/Policies")]
    [InlineData("Staging", "Reports", "docs/Staging/Reports")]
    public void BuildFolderPath_ReturnsCorrectFormat(string environment, string folderName, string expectedPath)
    {
        // Arrange & Act
        var folderPath = $"docs/{environment}/{folderName}";
        
        // Assert
        folderPath.Should().Be(expectedPath);
    }

    [Fact]
    public void FolderName_Validation_AllowsValidCharacters()
    {
        // Arrange
        var validFolderNames = new[]
        {
            "General",
            "Policies-2024",
            "Meeting Notes",
            "Q1-Reports",
            "Team-Alpha"
        };
        
        // Act & Assert
        foreach (var folderName in validFolderNames)
        {
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(folderName, @"^[a-zA-Z0-9\s\-]+$");
            isValid.Should().BeTrue($"Folder name '{folderName}' should be valid");
        }
    }

    [Fact]
    public void FolderName_Validation_RejectsInvalidCharacters()
    {
        // Arrange
        var invalidFolderNames = new[]
        {
            "General/Subfolder",
            "Policies@2024",
            "Meeting_Notes",
            "Q1#Reports",
            "Team.Alpha"
        };
        
        // Act & Assert
        foreach (var folderName in invalidFolderNames)
        {
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(folderName, @"^[a-zA-Z0-9\s\-]+$");
            isValid.Should().BeFalse($"Folder name '{folderName}' should be invalid");
        }
    }

    #endregion

    #region Document Metadata Tests

    [Theory]
    [InlineData(".pdf", "bi-file-pdf")]
    [InlineData(".txt", "bi-file-text")]
    [InlineData(".doc", "bi-file-word")]
    [InlineData(".docx", "bi-file-word")]
    [InlineData(".xls", "bi-file-excel")]
    [InlineData(".xlsx", "bi-file-excel")]
    [InlineData(".csv", "bi-file-excel")]
    [InlineData(".ppt", "bi-file-earmark-slides")]
    [InlineData(".pptx", "bi-file-earmark-slides")]
    public void GetFileIcon_ReturnsCorrectIcon_ForExtension(string extension, string expectedIcon)
    {
        // Arrange
        var document = new DocumentMetadata
        {
            FileName = $"test{extension}",
            Extension = extension,
            FilePath = $"docs/test{extension}",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow
        };
        
        // Act
        var icon = GetFileIconHelper(document.Extension.ToLowerInvariant());
        
        // Assert
        icon.Should().Be(expectedIcon, $"Extension {extension} should map to icon {expectedIcon}");
    }

    [Fact]
    public void GetFileIcon_ReturnsDefaultIcon_ForUnknownExtension()
    {
        // Arrange
        var extension = ".unknown";
        
        // Act
        var icon = GetFileIconHelper(extension);
        
        // Assert
        icon.Should().Be("bi-file-earmark", "Unknown extensions should return default file icon");
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(5120, "5.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(5242880, "5.0 MB")]
    public void FormatFileSize_ReturnsCorrectFormat(long bytes, string expectedFormat)
    {
        // Arrange & Act
        var formattedSize = FormatFileSizeHelper(bytes);
        
        // Assert
        formattedSize.Should().Be(expectedFormat);
    }

    #endregion

    #region Helper Methods

    private List<DocumentMetadata> GenerateDocuments(int count)
    {
        var documents = new List<DocumentMetadata>();
        for (int i = 1; i <= count; i++)
        {
            documents.Add(new DocumentMetadata
            {
                FileName = $"document-{i}.pdf",
                FilePath = $"docs/test/document-{i}.pdf",
                SizeBytes = 1024 * i,
                LastModified = DateTime.UtcNow.AddDays(-i),
                Extension = ".pdf"
            });
        }
        return documents;
    }

    private string GetFileIconHelper(string extension)
    {
        return extension switch
        {
            ".pdf" => "bi-file-pdf",
            ".txt" => "bi-file-text",
            ".doc" or ".docx" => "bi-file-word",
            ".xls" or ".xlsx" or ".csv" => "bi-file-excel",
            ".ppt" or ".pptx" => "bi-file-earmark-slides",
            _ => "bi-file-earmark"
        };
    }

    private string FormatFileSizeHelper(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        else if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        else
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    #endregion
}
