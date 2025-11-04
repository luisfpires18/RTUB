using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the TablePagination component to ensure pagination functionality works correctly
/// </summary>
public class TablePaginationTests : TestContext
{
    [Fact]
    public void TablePagination_RendersItemCount_Correctly()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        cut.Markup.Should().Contain("Mostrando 1-10 de 50", "should display correct item range");
    }

    [Fact]
    public void TablePagination_RendersWithCustomItemLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50)
            .Add(p => p.ItemLabel, "eventos"));

        // Assert
        cut.Markup.Should().Contain("eventos", "should display custom item label");
    }

    [Fact]
    public void TablePagination_DoesNotRenderPaginationNav_WhenOnlyOnePage()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 5));

        // Assert
        cut.Markup.Should().NotContain("page-item", "pagination navigation should not render when only one page");
        cut.Markup.Should().NotContain("Anterior", "previous button should not render when only one page");
        cut.Markup.Should().NotContain("Próximo", "next button should not render when only one page");
    }

    [Fact]
    public void TablePagination_RendersPaginationControls_WhenMultiplePages()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        cut.Markup.Should().Contain("pagination", "pagination controls should render when multiple pages");
    }

    [Fact]
    public void TablePagination_DisablesPreviousButton_OnFirstPage()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        var previousButton = cut.FindAll("button").First(b => b.TextContent.Contains("Anterior"));
        previousButton.GetAttribute("disabled").Should().NotBeNull("previous button should be disabled on first page");
    }

    [Fact]
    public void TablePagination_EnablesPreviousButton_OnSecondPage()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 2)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        var previousButton = cut.FindAll("button").First(b => b.TextContent.Contains("Anterior"));
        previousButton.GetAttribute("disabled").Should().BeNull("previous button should be enabled on second page");
    }

    [Fact]
    public void TablePagination_DisablesNextButton_OnLastPage()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 5)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        var nextButton = cut.FindAll("button").First(b => b.TextContent.Contains("Próximo"));
        nextButton.GetAttribute("disabled").Should().NotBeNull("next button should be disabled on last page");
    }

    [Fact]
    public void TablePagination_EnablesNextButton_BeforeLastPage()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        var nextButton = cut.FindAll("button").First(b => b.TextContent.Contains("Próximo"));
        nextButton.GetAttribute("disabled").Should().BeNull("next button should be enabled before last page");
    }

    [Fact]
    public void TablePagination_ShowsAllPages_When7OrFewerPages()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 70));

        // Assert
        for (int i = 1; i <= 7; i++)
        {
            cut.Markup.Should().Contain($">{i}<", $"page {i} should be visible");
        }
    }

    [Fact]
    public void TablePagination_ShowsEllipsis_WhenMoreThan7Pages()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 5)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 100));

        // Assert
        cut.Markup.Should().Contain("...", "ellipsis should be shown for many pages");
    }

    [Fact]
    public void TablePagination_HighlightsCurrentPage()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 3)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50));

        // Assert
        var pageItems = cut.FindAll("li.page-item");
        var activePageItem = pageItems.FirstOrDefault(li => li.ClassList.Contains("active"));
        activePageItem.Should().NotBeNull("there should be an active page item");
        activePageItem!.TextContent.Should().Contain("3", "current page should be highlighted");
    }

    [Fact]
    public void TablePagination_InvokesPageChangedCallback_OnPageClick()
    {
        // Arrange
        int receivedPage = 0;
        bool callbackInvoked = false;

        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50)
            .Add(p => p.OnPageChanged, EventCallback.Factory.Create<int>(this, (page) =>
            {
                callbackInvoked = true;
                receivedPage = page;
            })));

        // Act
        var pageButtons = cut.FindAll("button").Where(b => b.TextContent.Trim() == "2").ToList();
        if (pageButtons.Any())
        {
            pageButtons.First().Click();
        }

        // Assert
        callbackInvoked.Should().BeTrue("OnPageChanged callback should be invoked");
        receivedPage.Should().Be(2, "callback should receive the clicked page number");
    }

    [Fact]
    public void TablePagination_InvokesPageChangedCallback_OnNextClick()
    {
        // Arrange
        int receivedPage = 0;

        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50)
            .Add(p => p.OnPageChanged, EventCallback.Factory.Create<int>(this, (page) =>
            {
                receivedPage = page;
            })));

        // Act
        var nextButton = cut.FindAll("button").First(b => b.TextContent.Contains("Próximo"));
        nextButton.Click();

        // Assert
        receivedPage.Should().Be(2, "clicking next should go to page 2");
    }

    [Fact]
    public void TablePagination_InvokesPageChangedCallback_OnPreviousClick()
    {
        // Arrange
        int receivedPage = 0;

        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 2)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50)
            .Add(p => p.OnPageChanged, EventCallback.Factory.Create<int>(this, (page) =>
            {
                receivedPage = page;
            })));

        // Act
        var previousButton = cut.FindAll("button").First(b => b.TextContent.Contains("Anterior"));
        previousButton.Click();

        // Assert
        receivedPage.Should().Be(1, "clicking previous should go to page 1");
    }

    [Fact]
    public void TablePagination_RendersPageSizeSelector()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 25)
            .Add(p => p.TotalItems, 100));

        // Assert
        cut.Markup.Should().Contain("Itens por página:", "page size label should be displayed");
        var select = cut.Find("select");
        select.Should().NotBeNull("page size selector should exist");
    }

    [Fact]
    public void TablePagination_PageSizeSelector_HasCorrectOptions()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 25)
            .Add(p => p.TotalItems, 100));

        // Assert
        var select = cut.Find("select");
        select.InnerHtml.Should().Contain("value=\"10\"", "should have 10 option");
        select.InnerHtml.Should().Contain("value=\"25\"", "should have 25 option");
        select.InnerHtml.Should().Contain("value=\"50\"", "should have 50 option");
        select.InnerHtml.Should().Contain("value=\"100\"", "should have 100 option");
    }

    [Fact]
    public void TablePagination_PageSizeSelector_ShowsCurrentPageSize()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 50)
            .Add(p => p.TotalItems, 100));

        // Assert
        var select = cut.Find("select");
        select.GetAttribute("value").Should().Be("50", "page size selector should show current page size");
    }

    [Fact]
    public void TablePagination_InvokesPageSizeChangedCallback_OnSizeChange()
    {
        // Arrange
        int receivedPageSize = 0;
        bool callbackInvoked = false;

        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 25)
            .Add(p => p.TotalItems, 100)
            .Add(p => p.OnPageSizeChanged, EventCallback.Factory.Create<int>(this, (size) =>
            {
                callbackInvoked = true;
                receivedPageSize = size;
            })));

        // Act
        var select = cut.Find("select");
        select.Change("50");

        // Assert
        callbackInvoked.Should().BeTrue("OnPageSizeChanged callback should be invoked");
        receivedPageSize.Should().Be(50, "callback should receive the new page size");
    }

    [Fact]
    public void TablePagination_CalculatesEndItemNumber_Correctly()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 5)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 47)); // Last page with only 7 items

        // Assert
        cut.Markup.Should().Contain("Mostrando 41-47 de 47", "should calculate end item correctly on last page");
    }

    [Fact]
    public void TablePagination_ShowsZero_WhenNoItems()
    {
        // Arrange & Act
        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 1)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 0));

        // Assert
        cut.Markup.Should().Contain("Mostrando 0-0 de 0", "should show zero when no items");
    }

    [Fact]
    public void TablePagination_DoesNotInvokeCallback_WhenClickingSamePage()
    {
        // Arrange
        int callbackCount = 0;

        var cut = RenderComponent<TablePagination>(parameters => parameters
            .Add(p => p.CurrentPage, 2)
            .Add(p => p.PageSize, 10)
            .Add(p => p.TotalItems, 50)
            .Add(p => p.OnPageChanged, EventCallback.Factory.Create<int>(this, (page) =>
            {
                callbackCount++;
            })));

        // Act - Try to click the current page (page 2)
        var pageButtons = cut.FindAll("button").Where(b => b.TextContent.Trim() == "2").ToList();
        if (pageButtons.Any())
        {
            pageButtons.First().Click();
        }

        // Assert
        callbackCount.Should().Be(0, "callback should not be invoked when clicking the same page");
    }
}
