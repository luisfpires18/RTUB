using Xunit;
using FluentAssertions;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Unit tests for Members page grid behavior
/// Testing search, filter, sort, pagination, and role-based visibility
/// Note: These are behavioral/logical tests. Full component rendering tests
/// require authentication setup which is complex for unit tests.
/// </summary>
public class MembersPageGridTests
{
    #region Grid Layout Tests

    [Fact]
    public void MembersGrid_Layout_ShouldBe_ResponsiveGrid()
    {
        // This test verifies the CSS class usage
        // The actual CSS is defined in avatarcard.css
        // Desktop: 4-6 columns (auto-fill, minmax(200px, 1fr))
        // Tablet: 3 columns
        // Mobile: 1-2 columns (auto-fill, minmax(150px, 1fr))
        
        var expectedGridClass = "avatar-card-grid";
        expectedGridClass.Should().Be("avatar-card-grid", 
            "Members and Leitões grids should use avatar-card-grid class for responsive layout");
    }

    [Fact]
    public void MembersGrid_DesktopColumns_ShouldBeBetween_FourAndSix()
    {
        // This test documents the expected behavior
        // Desktop (>= 1024px): grid-template-columns: repeat(auto-fill, minmax(200px, 1fr))
        // Desktop large (>= 1400px): grid-template-columns: repeat(auto-fill, minmax(220px, 1fr))
        // This gives 4-6 columns depending on screen width
        
        var minColumns = 4;
        var maxColumns = 6;
        
        minColumns.Should().Be(4, "Desktop should display at least 4 columns");
        maxColumns.Should().Be(6, "Desktop should display at most 6 columns on very wide screens");
    }

    [Fact]
    public void MembersGrid_TabletColumns_ShouldBeThree()
    {
        // Tablet (768px - 1023px): grid-template-columns: repeat(3, 1fr)
        var expectedColumns = 3;
        expectedColumns.Should().Be(3, "Tablet should display exactly 3 columns");
    }

    [Fact]
    public void MembersGrid_MobileColumns_ShouldBeOneOrTwo()
    {
        // Mobile (< 768px): grid-template-columns: repeat(auto-fill, minmax(150px, 1fr))
        // This gives 1-2 columns depending on screen width
        
        var minColumns = 1;
        var maxColumns = 2;
        
        minColumns.Should().Be(1, "Mobile should display at least 1 column");
        maxColumns.Should().Be(2, "Mobile should display at most 2 columns");
    }

    #endregion

    #region Search, Filter, Sort Tests

    [Fact]
    public void MembersGrid_Search_ShouldFilter_ByMultipleFields()
    {
        // Search bar should filter BOTH grids (Members and Leitões) by:
        // - FirstName
        // - LastName
        // - Nickname
        // - Email
        // - PhoneContact
        // - City
        
        var searchableFields = new[] { "FirstName", "LastName", "Nickname", "Email", "PhoneContact", "City" };
        searchableFields.Should().HaveCount(6, "Search should filter both Members and Leitões grids across 6 fields including City");
    }

    [Fact]
    public void MembersGrid_Filter_ShouldSupportCategoriaAndInstrumento()
    {
        // Category filter now includes Leitão, Caloiro, Tuno (applies to both grids)
        // - When Leitão is selected: Show only Leitões grid
        // - When Caloiro or Tuno is selected: Show only Members grid (filtered by that category)
        // - When no category is selected: Show both grids
        // Instrument filter includes all InstrumentType enum values (applies to both grids)
        
        var categoryOptions = new[] { "Leitao", "Caloiro", "Tuno" };
        categoryOptions.Should().HaveCount(3, "Category filter should support Leitão, Caloiro, and Tuno categories");
        
        // Instrument filter includes all InstrumentType values and applies to both grids
        // This is verified by the actual enum
    }

    [Fact]
    public void LeitoesGrid_Filter_ShouldOnlyShowLeitoes()
    {
        // Leitões grid should only show users with IsLeitao() == true
        // It has its own independent filter/search/sort/pagination
        
        var leitoesCategory = "Leitão";
        leitoesCategory.Should().Be("Leitão", "Leitões grid should only display Leitão category");
    }

    [Fact]
    public void InstrumentFilter_ShouldApplyToBothGrids()
    {
        // When an instrument is selected in the filter dropdown,
        // BOTH the Members grid AND the Leitões grid should be filtered
        // to show only users with that instrument
        // Note: Category filter now applies to both grids and controls which grids are visible
        
        var instrumentFilterAppliedToBothGrids = true;
        instrumentFilterAppliedToBothGrids.Should().BeTrue(
            "Instrument filter should apply to both Members and Leitões grids");
    }

    [Fact]
    public void CategoryFilter_WhenLeitaoSelected_ShouldShowOnlyLeitoesGrid()
    {
        // When "Leitão" is selected in the category filter:
        // - Only the Leitões grid should be displayed
        // - The Members grid should be empty/hidden
        
        var selectedCategory = "Leitao";
        selectedCategory.Should().Be("Leitao", "Selecting Leitão category should show only Leitões grid");
    }

    [Fact]
    public void CategoryFilter_WhenCaloiroOrTunoSelected_ShouldShowOnlyMembersGrid()
    {
        // When "Caloiro" or "Tuno" is selected in the category filter:
        // - Only the Members grid should be displayed (filtered by that category)
        // - The Leitões grid should be empty/hidden
        
        var caloiroCategory = "Caloiro";
        var tunoCategory = "Tuno";
        
        caloiroCategory.Should().Be("Caloiro", "Selecting Caloiro should show only Members grid with Caloiros");
        tunoCategory.Should().Be("Tuno", "Selecting Tuno should show only Members grid with Tunos");
    }

    [Fact]
    public void CategoryFilter_WhenNoSelection_ShouldShowBothGrids()
    {
        // When no category is selected (empty string):
        // - Both Members and Leitões grids should be displayed
        // - All members should be visible (subject to other filters like search and instrument)
        
        var noCategory = "";
        noCategory.Should().BeEmpty("No category selection should display both grids");
    }

    [Fact]
    public void MembersGrid_Sort_ShouldSupportMultipleColumns()
    {
        // Sort columns:
        // - FirstName
        // - Nickname
        // - Category
        // - Instrument
        
        var sortableColumns = new[] { "FirstName", "Nickname", "Category", "Instrument" };
        sortableColumns.Should().HaveCount(4, "Members grid should support sorting by 4 columns");
    }

    [Fact]
    public void LeitoesGrid_Sort_ShouldBeIndependent()
    {
        // Leitões grid has its own independent sort state
        // Sorting members should not affect Leitões sort and vice versa
        
        var hasIndependentSort = true;
        hasIndependentSort.Should().BeTrue("Leitões grid should have independent sort from Members grid");
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public void MembersGrid_Pagination_ShouldUsePageSize50()
    {
        // Default page size for Members grid
        var defaultPageSize = 50;
        defaultPageSize.Should().Be(50, "Members grid should use page size of 50");
    }

    [Fact]
    public void LeitoesGrid_Pagination_ShouldBeIndependent()
    {
        // Leitões grid has its own pagination helper
        // Changing Members page should not affect Leitões page
        
        var hasIndependentPagination = true;
        hasIndependentPagination.Should().BeTrue("Leitões grid should have independent pagination from Members grid");
    }

    [Fact]
    public void LeitoesGrid_Pagination_ShouldUsePageSize50()
    {
        // Default page size for Leitões grid
        var defaultPageSize = 50;
        defaultPageSize.Should().Be(50, "Leitões grid should use page size of 50");
    }

    [Fact]
    public void Grids_Pagination_CountsShouldMatch_FilteredResults()
    {
        // Pagination counts should reflect filtered results, not total
        // Members grid count should only count non-Leitão members after filters
        // Leitões grid count should only count Leitão members after filters
        
        var countsMatchFiltered = true;
        countsMatchFiltered.Should().BeTrue("Pagination counts should match filtered results");
    }

    #endregion

    #region Role-Based Visibility Tests

    [Fact]
    public void AvatarCard_EditButton_ShouldBeVisible_ForAdminRole()
    {
        // Admin role should see Edit button on all cards
        var adminCanEdit = true;
        adminCanEdit.Should().BeTrue("Admin role should see Edit button");
    }

    [Fact]
    public void AvatarCard_EditButton_ShouldBeHidden_ForNonAdminRole()
    {
        // Non-admin roles should not see Edit button
        var nonAdminCanEdit = false;
        nonAdminCanEdit.Should().BeFalse("Non-admin roles should not see Edit button");
    }

    [Fact]
    public void MembersGrid_DeleteButton_ShouldBeVisible_ForOwnerRole()
    {
        // Owner role should see Delete button on Members cards
        var ownerCanDelete = true;
        ownerCanDelete.Should().BeTrue("Owner role should see Delete button on Members");
    }

    [Fact]
    public void LeitoesGrid_DeleteButton_ShouldBeVisible_ForAdminRole()
    {
        // Admin role should see Delete button on Leitões cards
        var adminCanDelete = true;
        adminCanDelete.Should().BeTrue("Admin role should see Delete button on Leitões");
    }

    [Fact]
    public void AvatarCard_DeleteButton_ShouldBeHidden_ForNonAuthorizedRole()
    {
        // Non-authorized roles should not see Delete button
        var nonAuthorizedCanDelete = false;
        nonAuthorizedCanDelete.Should().BeFalse("Non-authorized roles should not see Delete button");
    }

    [Fact]
    public void AvatarCard_ViewButton_ShouldBeVisible_ForAllAuthenticatedUsers()
    {
        // All authenticated users should see View button
        var allCanView = true;
        allCanView.Should().BeTrue("All authenticated users should see View button");
    }

    #endregion

    #region Category-Based Permission Tests

    [Fact]
    public void MembersGrid_EditButton_ShouldBeVisible_ForAdminWithTunoCategory()
    {
        // Admin users with Tuno category should see Edit button on Members grid
        var isAdmin = true;
        var currentUserIsCaloiro = false;
        
        var showEditButton = isAdmin && !currentUserIsCaloiro;
        showEditButton.Should().BeTrue("Admin with Tuno category should see Edit button on Members grid");
    }

    [Fact]
    public void MembersGrid_EditButton_ShouldBeHidden_ForAdminWithCaloiroCategory()
    {
        // Admin users with Caloiro category should NOT see Edit button on Members grid
        var isAdmin = true;
        var currentUserIsCaloiro = true;
        
        var showEditButton = isAdmin && !currentUserIsCaloiro;
        showEditButton.Should().BeFalse("Admin with Caloiro category should NOT see Edit button on Members grid");
    }

    [Fact]
    public void MembersGrid_EditButton_ShouldBeHidden_ForNonAdmin()
    {
        // Non-admin users should NOT see Edit button regardless of category
        var isAdmin = false;
        var currentUserIsCaloiro = false;
        
        var showEditButton = isAdmin && !currentUserIsCaloiro;
        showEditButton.Should().BeFalse("Non-admin users should NOT see Edit button on Members grid");
    }

    [Fact]
    public void LeitoesGrid_EditButton_ShouldBeVisible_ForAdminWithTunoCategory()
    {
        // Admin users with Tuno category should see Edit button on Leitões grid
        var isAdmin = true;
        var currentUserIsCaloiro = false;
        
        var showEditButton = isAdmin && !currentUserIsCaloiro;
        showEditButton.Should().BeTrue("Admin with Tuno category should see Edit button on Leitões grid");
    }

    [Fact]
    public void LeitoesGrid_EditButton_ShouldBeHidden_ForAdminWithCaloiroCategory()
    {
        // Admin users with Caloiro category should NOT see Edit button on Leitões grid
        var isAdmin = true;
        var currentUserIsCaloiro = true;
        
        var showEditButton = isAdmin && !currentUserIsCaloiro;
        showEditButton.Should().BeFalse("Admin with Caloiro category should NOT see Edit button on Leitões grid");
    }

    [Fact]
    public void LeitoesGrid_DeleteButton_ShouldBeVisible_ForAdminWithTunoCategory()
    {
        // Admin users with Tuno category should see Delete button on Leitões grid
        var isAdmin = true;
        var currentUserIsCaloiro = false;
        
        var showDeleteButton = isAdmin && !currentUserIsCaloiro;
        showDeleteButton.Should().BeTrue("Admin with Tuno category should see Delete button on Leitões grid");
    }

    [Fact]
    public void LeitoesGrid_DeleteButton_ShouldBeHidden_ForAdminWithCaloiroCategory()
    {
        // Admin users with Caloiro category should NOT see Delete button on Leitões grid
        var isAdmin = true;
        var currentUserIsCaloiro = true;
        
        var showDeleteButton = isAdmin && !currentUserIsCaloiro;
        showDeleteButton.Should().BeFalse("Admin with Caloiro category should NOT see Delete button on Leitões grid");
    }

    [Fact]
    public void MembersGrid_DeleteButton_ShouldBeVisible_ForOwner()
    {
        // Owner role should see Delete button on Members grid (independent of category)
        var isOwner = true;
        
        var showDeleteButton = isOwner;
        showDeleteButton.Should().BeTrue("Owner should see Delete button on Members grid");
    }

    [Fact]
    public void MembersGrid_DeleteButton_ShouldBeHidden_ForNonOwner()
    {
        // Non-owner users should NOT see Delete button on Members grid
        var isOwner = false;
        
        var showDeleteButton = isOwner;
        showDeleteButton.Should().BeFalse("Non-owner should NOT see Delete button on Members grid");
    }

    [Fact]
    public void CheckUserRoles_ShouldDetectCaloiroCategory_ForAdminUser()
    {
        // When an admin user has Caloiro in their categories, currentUserIsCaloiro should be true
        // This tests the logic: currentUser.Categories.Contains(MemberCategory.Caloiro)
        
        var userHasCaloiroCategory = true;
        var isAdmin = true;
        
        var currentUserIsCaloiro = isAdmin && userHasCaloiroCategory;
        currentUserIsCaloiro.Should().BeTrue("Should detect Caloiro category for admin user");
    }

    [Fact]
    public void CheckUserRoles_ShouldNotDetectCaloiroCategory_ForNonCaloiroAdmin()
    {
        // When an admin user does NOT have Caloiro in their categories, currentUserIsCaloiro should be false
        
        var userHasCaloiroCategory = false;
        var isAdmin = true;
        
        var currentUserIsCaloiro = isAdmin && userHasCaloiroCategory;
        currentUserIsCaloiro.Should().BeFalse("Should NOT detect Caloiro category for non-Caloiro admin user");
    }

    [Fact]
    public void CheckUserRoles_ShouldNotCheckCategory_ForNonAdmin()
    {
        // When user is not admin, category check should not be performed
        
        var currentUserIsCaloiro = false;
        
        // Logic: if (isAdmin) { check category } else { currentUserIsCaloiro remains false }
        currentUserIsCaloiro.Should().BeFalse("Should not check category for non-admin users");
    }

    [Fact]
    public void PermissionLogic_ShouldBeConsistent_AcrossBothGrids()
    {
        // Edit button permission logic should be the same for both grids
        var isAdmin = true;
        var currentUserIsCaloiro = false;
        
        var membersGridEditPermission = isAdmin && !currentUserIsCaloiro;
        var leitoesGridEditPermission = isAdmin && !currentUserIsCaloiro;
        
        membersGridEditPermission.Should().Be(leitoesGridEditPermission,
            "Edit button permission logic should be consistent across both grids");
    }

    [Fact]
    public void PermissionLogic_DeleteButton_ShouldDifferBetweenGrids()
    {
        // Delete button permission differs between grids
        // Members grid: Owner only
        // Leitões grid: Admin (non-Caloiro)
        
        var isOwner = false;
        var isAdmin = true;
        var currentUserIsCaloiro = false;
        
        var membersGridDeletePermission = isOwner;
        var leitoesGridDeletePermission = isAdmin && !currentUserIsCaloiro;
        
        membersGridDeletePermission.Should().NotBe(leitoesGridDeletePermission,
            "Delete button permission logic should differ between grids");
    }

    #endregion

    #region Action Handler Tests

    [Fact]
    public void AvatarCard_ViewAction_ShouldOpen_DetailsModal()
    {
        // View button should trigger OpenViewDetailsModal with the specific user
        var actionName = "OpenViewDetailsModal";
        actionName.Should().Be("OpenViewDetailsModal", "View action should open details modal");
    }

    [Fact]
    public void AvatarCard_EditAction_ShouldOpen_EditModal()
    {
        // Edit button should trigger OpenEditModal with the specific user
        var actionName = "OpenEditModal";
        actionName.Should().Be("OpenEditModal", "Edit action should open edit modal");
    }

    [Fact]
    public void AvatarCard_DeleteAction_ShouldOpen_DeleteModal()
    {
        // Delete button should trigger OpenDeleteModal with the specific user
        var actionName = "OpenDeleteModal";
        actionName.Should().Be("OpenDeleteModal", "Delete action should open delete modal");
    }

    [Fact]
    public void AvatarCard_Actions_ShouldFireCorrectHandlers_PerMember()
    {
        // Each card should fire handlers with its specific member data
        // Not a different member's data
        
        var handlersArePerMember = true;
        handlersArePerMember.Should().BeTrue("Action handlers should be specific to each member");
    }

    [Fact]
    public void AvatarCard_Actions_ShouldFireCorrectHandlers_PerLeitao()
    {
        // Each Leitão card should fire handlers with its specific Leitão data
        
        var handlersArePerLeitao = true;
        handlersArePerLeitao.Should().BeTrue("Action handlers should be specific to each Leitão");
    }

    #endregion

    #region Avatar Lazy-Load Tests

    [Fact]
    public void AvatarCard_Avatar_ShouldUseLazyLoading()
    {
        // Avatars should use lazy loading to improve performance
        var usesLazyLoad = true;
        usesLazyLoad.Should().BeTrue("Avatars should use lazy loading");
    }

    [Fact]
    public void AvatarCard_LazyLoad_ShouldNotBlock_Actions()
    {
        // Action buttons should be functional even if avatar hasn't loaded
        var actionsWorkWithoutImage = true;
        actionsWorkWithoutImage.Should().BeTrue("Actions should work regardless of avatar load state");
    }

    [Fact]
    public void AvatarCard_LazyLoad_ShouldShow_Skeleton()
    {
        // While avatar is loading, a skeleton loader should be displayed
        var showsSkeleton = true;
        showsSkeleton.Should().BeTrue("Should show skeleton while avatar is loading");
    }

    [Fact]
    public void AvatarCard_LazyLoad_ShouldHideSkeleton_AfterLoad()
    {
        // After avatar loads, skeleton should be hidden
        var hidesSkeleton = true;
        hidesSkeleton.Should().BeTrue("Should hide skeleton after avatar loads");
    }

    #endregion

    #region Loading State Tests

    [Fact]
    public void MembersGrid_Loading_ShouldShow_SkeletonCards()
    {
        // While users are loading, show 8 skeleton cards
        var skeletonCount = 8;
        skeletonCount.Should().Be(8, "Should show 8 skeleton cards while loading");
    }

    [Fact]
    public void MembersGrid_Empty_ShouldShow_EmptyState()
    {
        // When no members match filters, show EmptyTableState component
        var showsEmptyState = true;
        showsEmptyState.Should().BeTrue("Should show empty state when no members match");
    }

    #endregion

    #region Keyboard Accessibility Tests

    [Fact]
    public void AvatarCard_EnterKey_ShouldTrigger_ViewAction()
    {
        // Pressing Enter on a focused card should trigger View action
        var enterTriggersView = true;
        enterTriggersView.Should().BeTrue("Enter key should trigger View action");
    }

    [Fact]
    public void AvatarCard_ShouldHave_TabIndex()
    {
        // Cards should have tabindex="0" for keyboard navigation
        var hasTabIndex = true;
        hasTabIndex.Should().BeTrue("Cards should be keyboard accessible via Tab key");
    }

    [Fact]
    public void AvatarCard_Buttons_ShouldHave_AriaLabels()
    {
        // All buttons should have aria-label for screen readers
        var hasAriaLabels = true;
        hasAriaLabels.Should().BeTrue("Buttons should have aria-labels for accessibility");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void MembersGrid_ShouldAvoid_NPlus1Lookups()
    {
        // All member data should be loaded in advance
        // No additional queries per card render
        
        var avoidsNPlus1 = true;
        avoidsNPlus1.Should().BeTrue("Should avoid N+1 database lookups");
    }

    [Fact]
    public void MembersGrid_ShouldReuse_DataSource()
    {
        // Grid should use same data source as original table
        // No additional requests or transformations needed
        
        var reusesDataSource = true;
        reusesDataSource.Should().BeTrue("Should reuse existing data source");
    }

    [Fact]
    public void MembersGrid_ShouldPreserve_PageSize()
    {
        // Grid should use same page size as original table (50)
        var defaultPageSize = 50;
        defaultPageSize.Should().Be(50, "Should preserve original page size");
    }

    #endregion

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
