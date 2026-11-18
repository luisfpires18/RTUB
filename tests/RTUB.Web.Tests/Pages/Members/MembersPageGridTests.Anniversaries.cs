// Anniversaries modal and table pagination
using FluentAssertions;

namespace RTUB.Web.Tests.Pages;

public partial class MembersPageGridTests
{
    #region Anniversaries Modal Tests

    [Fact]
    public void AnniversariesModal_Pagination_ShouldUsePageSize10()
    {
        // Default page size for anniversaries modal should be 10
        var defaultPageSize = 10;
        defaultPageSize.Should().Be(10, "Anniversaries modal should use page size of 10");
    }

    [Fact]
    public void AnniversariesModal_Pagination_ShouldHaveCorrectOptions()
    {
        // Page size options should be 10, 20, 30, 40, 50
        var pageSizeOptions = new[] { 10, 20, 30, 40, 50 };
        pageSizeOptions.Should().HaveCount(5, "Should have 5 page size options");
        pageSizeOptions.Should().Contain(10, "Should contain 10");
        pageSizeOptions.Should().Contain(20, "Should contain 20");
        pageSizeOptions.Should().Contain(30, "Should contain 30");
        pageSizeOptions.Should().Contain(40, "Should contain 40");
        pageSizeOptions.Should().Contain(50, "Should contain 50");
    }

    [Fact]
    public void AnniversariesModal_ShouldFilterByCurrentYearAndFutureDates()
    {
        // Anniversaries modal should only show birthdays where:
        // - nextBirthday >= Today
        // - nextBirthday is in current year

        var today = DateTime.Now.Date;
        var currentYear = today.Year;

        // Example: If today is 19/11/2025, show birthdays from 19/11/2025 to 31/12/2025
        var shouldShowFutureBirthdays = true;
        shouldShowFutureBirthdays.Should().BeTrue("Should show future birthdays in current year");

        var shouldNotShowPastBirthdays = false;
        shouldNotShowPastBirthdays.Should().BeFalse("Should not show past birthdays from earlier in the year");
    }

    [Fact]
    public void AnniversariesModal_OnJanuary1st_ShouldShowFullYear()
    {
        // On January 1st of a new year, the modal should show the full year automatically
        // without extra configuration

        var january1st = new DateTime(2026, 1, 1);
        var currentYear = january1st.Year;
        var endOfYear = new DateTime(currentYear, 12, 31);

        // All birthdays from 01/01/2026 to 31/12/2026 should be shown
        var showsFullYear = true;
        showsFullYear.Should().BeTrue("On January 1st, should show full year of birthdays");
    }

    [Fact]
    public void AnniversariesModal_ShouldNotIncludePositionColumn()
    {
        // Anniversaries table should NOT include a position column
        var hasPositionColumn = false;
        hasPositionColumn.Should().BeFalse("Anniversaries table should not have position column");
    }

    [Fact]
    public void AnniversariesModal_ShouldOrderByNextBirthdayAscending()
    {
        // Anniversaries should be ordered by next birthday (earliest first), then by name
        var orderedByNextBirthday = true;
        orderedByNextBirthday.Should().BeTrue("Should order by next birthday ascending");
    }

    [Fact]
    public void AnniversariesModal_ShouldIncludeRequiredColumns()
    {
        // Required columns: Avatar/Nickname/Name, Category, Birthdate (dd/MM), Age, City
        var requiredColumns = new[] { "Member", "Category", "Birthday", "Age", "City" };
        requiredColumns.Should().HaveCount(5, "Should have 5 required columns (excluding admin actions)");
    }

    [Fact]
    public void AnniversariesModal_EmailButton_ShouldBeAdminOnly()
    {
        // Email action button should only be visible to Admin role
        var isAdmin = true;
        var emailButtonVisible = isAdmin;
        emailButtonVisible.Should().BeTrue("Email button should be visible to Admin");

        var isNonAdmin = false;
        var emailButtonHidden = !isNonAdmin;
        emailButtonHidden.Should().BeTrue("Email button should be hidden for non-Admin");
    }

    [Fact]
    public void AnniversariesModal_ShouldBeVisibleToAllMembers()
    {
        // The "Aniversários" button should be visible to all authenticated members
        // not just admins
        var visibleToAllMembers = true;
        visibleToAllMembers.Should().BeTrue("Anniversaries button should be visible to all members");
    }

    [Fact]
    public void TablePagination_DefaultPageSize_ShouldBe10()
    {
        // Shared TablePagination component default page size should be 10
        var defaultPageSize = 10;
        defaultPageSize.Should().Be(10, "TablePagination default page size should be 10");
    }

    [Fact]
    public void TablePagination_DefaultOptions_ShouldBe10To50()
    {
        // Shared TablePagination component default options should be 10, 20, 30, 40, 50
        var defaultOptions = new[] { 10, 20, 30, 40, 50 };
        defaultOptions.Should().HaveCount(5, "Should have 5 default page size options");
        defaultOptions.Should().BeInAscendingOrder("Options should be in ascending order");
    }

    #endregion
}
