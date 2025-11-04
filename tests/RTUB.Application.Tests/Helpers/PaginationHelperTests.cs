using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for PaginationHelper
/// Tests pagination logic and calculations
/// </summary>
public class PaginationHelperTests
{
    [Fact]
    public void GetPageData_WithValidData_ReturnsPaginatedData()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();

        // Act
        var result = helper.GetPageData(data);

        // Assert
        result.Should().HaveCount(10);
        result.Should().BeEquivalentTo(Enumerable.Range(1, 10));
        helper.TotalItems.Should().Be(25);
        helper.TotalPages.Should().Be(3);
    }

    [Fact]
    public void GetPageData_SecondPage_ReturnsCorrectData()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 2, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();

        // Act
        var result = helper.GetPageData(data);

        // Assert
        result.Should().HaveCount(10);
        result.Should().BeEquivalentTo(Enumerable.Range(11, 10));
    }

    [Fact]
    public void GetPageData_LastPage_ReturnsRemainingItems()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 3, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();

        // Act
        var result = helper.GetPageData(data);

        // Assert
        result.Should().HaveCount(5);
        result.Should().BeEquivalentTo(Enumerable.Range(21, 5));
    }

    [Fact]
    public void GetPageData_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };
        var data = new List<int>();

        // Act
        var result = helper.GetPageData(data);

        // Assert
        result.Should().BeEmpty();
        helper.TotalItems.Should().Be(0);
        helper.TotalPages.Should().Be(1);
        helper.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void GetPageData_PageBeyondTotal_AdjustsToLastPage()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 10, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();

        // Act
        var result = helper.GetPageData(data);

        // Assert
        helper.CurrentPage.Should().Be(3);
        result.Should().HaveCount(5);
    }

    [Fact]
    public void NextPage_WhenNotOnLastPage_IncrementPage()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act
        var result = helper.NextPage();

        // Assert
        result.Should().BeTrue();
        helper.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void NextPage_WhenOnLastPage_ReturnsFalse()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 3, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act
        var result = helper.NextPage();

        // Assert
        result.Should().BeFalse();
        helper.CurrentPage.Should().Be(3);
    }

    [Fact]
    public void PreviousPage_WhenNotOnFirstPage_DecrementPage()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 2, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act
        var result = helper.PreviousPage();

        // Assert
        result.Should().BeTrue();
        helper.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void PreviousPage_WhenOnFirstPage_ReturnsFalse()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };

        // Act
        var result = helper.PreviousPage();

        // Assert
        result.Should().BeFalse();
        helper.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void GoToPage_WithValidPage_NavigatesToPage()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act
        var result = helper.GoToPage(2);

        // Assert
        result.Should().BeTrue();
        helper.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void GoToPage_WithInvalidPage_ReturnsFalse()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act
        var result = helper.GoToPage(10);

        // Assert
        result.Should().BeFalse();
        helper.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void HasNextPage_WhenNotOnLastPage_ReturnsTrue()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act & Assert
        helper.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenOnLastPage_ReturnsFalse()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 3, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act & Assert
        helper.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenNotOnFirstPage_ReturnsTrue()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 2, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act & Assert
        helper.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void HasPreviousPage_WhenOnFirstPage_ReturnsFalse()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 10 };

        // Act & Assert
        helper.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void StartItemNumber_CalculatesCorrectly()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 2, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act & Assert
        helper.StartItemNumber.Should().Be(11);
    }

    [Fact]
    public void EndItemNumber_CalculatesCorrectly()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 2, PageSize = 10 };
        var data = Enumerable.Range(1, 25).ToList();
        helper.GetPageData(data);

        // Act & Assert
        helper.EndItemNumber.Should().Be(20);
    }

    [Fact]
    public void Reset_ResetsToDefaults()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 5, PageSize = 10 };
        var data = Enumerable.Range(1, 100).ToList();
        helper.GetPageData(data);

        // Act
        helper.Reset();

        // Assert
        helper.CurrentPage.Should().Be(1);
        helper.TotalPages.Should().Be(1);
        helper.TotalItems.Should().Be(0);
    }

    [Fact]
    public void GetPageData_WithPageSizeOne_WorksCorrectly()
    {
        // Arrange
        var helper = new PaginationHelper<int> { CurrentPage = 1, PageSize = 1 };
        var data = Enumerable.Range(1, 5).ToList();

        // Act
        var result = helper.GetPageData(data);

        // Assert
        result.Should().HaveCount(1);
        result.First().Should().Be(1);
        helper.TotalPages.Should().Be(5);
    }
}
