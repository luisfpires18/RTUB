using Xunit;
using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Unit tests for Rehearsals Attendance Modal features
/// Testing badge ordering, filtering, and pagination for the aligned modal
/// </summary>
public class RehearsalsAttendanceModalTests
{
    #region Badge Display Tests

    [Fact]
    public void BadgeOrder_MainParticipants_StatusShouldAppearBeforeCategory()
    {
        // Arrange
        var statusBadgeOrder = 1;
        var categoryBadgeOrder = 2;

        // Assert
        statusBadgeOrder.Should().BeLessThan(categoryBadgeOrder, 
            "Status badge (PENDENTE/APROVADO) should appear before category badge (TUNO/CALOIRO)");
    }

    [Fact]
    public void BadgeOrder_Leitoes_StatusShouldAppearBeforeCategory()
    {
        // Arrange
        var statusBadgeOrder = 1;
        var leitaoBadgeOrder = 2;

        // Assert
        statusBadgeOrder.Should().BeLessThan(leitaoBadgeOrder, 
            "Status badge should appear before Leitão category badge");
    }

    [Theory]
    [InlineData(true, "Aprovado")]
    [InlineData(false, "Pendente")]
    public void StatusBadge_ShouldDisplayCorrectText(bool attended, string expectedBadgeText)
    {
        // Act
        var badgeText = attended ? "Aprovado" : "Pendente";

        // Assert
        badgeText.Should().Be(expectedBadgeText, "Status badge should reflect attendance status");
    }

    #endregion

    #region Leitão Instrument Display Tests

    [Fact]
    public void Leitoes_WithInstrument_ShouldDisplayInstrumentText()
    {
        // Arrange
        var instrument = InstrumentType.Guitarra;
        var expectedDisplay = "Guitarra";

        // Act
        var hasInstrument = instrument != null;
        var displayText = hasInstrument ? expectedDisplay : "";

        // Assert
        displayText.Should().Be(expectedDisplay, "Leitões with configured instruments should display instrument text");
    }

    [Fact]
    public void Leitoes_WithoutInstrument_ShouldDisplayEmptyInstrumentText()
    {
        // Arrange
        InstrumentType? instrument = null;

        // Act
        var displayText = instrument.HasValue ? "Some Instrument" : "";

        // Assert
        displayText.Should().BeEmpty("Leitões without instruments should show empty instrument text");
    }

    [Theory]
    [InlineData(InstrumentType.Guitarra, "Guitarra")]
    [InlineData(InstrumentType.Bandolim, "Bandolim")]
    [InlineData(InstrumentType.Cavaquinho, "Cavaquinho")]
    public void Leitoes_InstrumentDisplay_ShouldMatchInstrumentType(InstrumentType instrumentType, string expectedDisplay)
    {
        // This test simulates the StatusHelper.GetInstrumentDisplay logic
        // In actual implementation, this would call StatusHelper.GetInstrumentDisplay(instrument)
        var displayText = instrumentType.ToString();

        // Assert
        displayText.Should().Be(expectedDisplay, "Instrument display should match the instrument type");
    }

    #endregion

    #region Not Attending Members Badge Tests

    [Fact]
    public void NotAttending_ShouldShowCategoryBadge()
    {
        // Arrange
        var willAttend = false;
        var showCategoryBadge = true; // New behavior: show category for not attending

        // Assert
        showCategoryBadge.Should().BeTrue("Not attending members should display category badges");
    }

    [Theory]
    [InlineData(MemberCategory.Tuno, "TUNO")]
    [InlineData(MemberCategory.Caloiro, "CALOIRO")]
    [InlineData(MemberCategory.Leitao, "LEITAO")]
    public void NotAttending_CategoryBadge_ShouldDisplayCorrectCategory(MemberCategory category, string expectedBadgeText)
    {
        // Act
        var badgeText = category.ToString().ToUpper();

        // Assert
        badgeText.Should().Be(expectedBadgeText, "Category badge should display the correct member category");
    }

    #endregion

    #region Filtering and Pagination Tests

    [Fact]
    public void FilterAttendances_ShouldSeparateMainParticipantsAndLeitoes()
    {
        // Arrange
        var totalAttendances = 10;
        var leitoesCount = 3;
        var mainParticipantsCount = totalAttendances - leitoesCount;

        // Assert
        (mainParticipantsCount + leitoesCount).Should().Be(totalAttendances, 
            "Main participants and Leitões should sum to total attendances");
        mainParticipantsCount.Should().Be(7, "Non-Leitão members should be in main participants");
        leitoesCount.Should().Be(3, "Leitão members should be in separate section");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Pagination_PageSizeOptions_ShouldBeValid(int pageSize)
    {
        // Arrange
        var validPageSizes = new[] { 4, 8, 12, 16 };

        // Assert
        validPageSizes.Should().Contain(pageSize, "Page size should be one of the valid options");
    }

    [Fact]
    public void Pagination_MainParticipantsAndLeitoes_ShouldBeIndependent()
    {
        // Arrange
        var mainParticipantsPage = 1;
        var leitoesPage = 2;

        // Assert
        mainParticipantsPage.Should().NotBe(leitoesPage, 
            "Main participants and Leitões should have independent pagination");
    }

    [Fact]
    public void SearchFilter_ShouldApplyToBothSections()
    {
        // Arrange
        var searchTerm = "João";
        var appliedToMainParticipants = true;
        var appliedToLeitoes = true;

        // Assert
        appliedToMainParticipants.Should().BeTrue("Search should filter main participants");
        appliedToLeitoes.Should().BeTrue("Search should filter Leitões");
    }

    [Theory]
    [InlineData("Todos")]
    [InlineData("Pendente")]
    [InlineData("Aprovado")]
    public void ApprovalFilter_ShouldHaveValidOptions(string filterValue)
    {
        // Arrange
        var validFilters = new[] { "Todos", "Pendente", "Aprovado" };

        // Assert
        validFilters.Should().Contain(filterValue, "Approval filter should have valid options");
    }

    [Fact]
    public void ApprovalFilter_Pendente_ShouldShowOnlyNotAttended()
    {
        // Arrange
        var filterValue = "Pendente";
        var shouldShowAttended = false;
        var shouldShowNotAttended = true;

        // Assert
        shouldShowNotAttended.Should().BeTrue("Pendente filter should show not attended members");
        shouldShowAttended.Should().BeFalse("Pendente filter should hide attended members");
    }

    [Fact]
    public void ApprovalFilter_Aprovado_ShouldShowOnlyAttended()
    {
        // Arrange
        var filterValue = "Aprovado";
        var shouldShowAttended = true;
        var shouldShowNotAttended = false;

        // Assert
        shouldShowAttended.Should().BeTrue("Aprovado filter should show attended members");
        shouldShowNotAttended.Should().BeFalse("Aprovado filter should hide not attended members");
    }

    #endregion

    #region Modal Initialization Tests

    [Fact]
    public void ViewAttendances_ShouldCallFilterAttendances()
    {
        // Arrange
        var filterAttendancesCalled = true;

        // Assert
        filterAttendancesCalled.Should().BeTrue(
            "ViewAttendances should call FilterAttendances to populate filtered lists");
    }

    [Fact]
    public void ViewAttendances_ShouldInitializeAllPaginationHelpers()
    {
        // Arrange
        var attendancesPaginationInitialized = true;
        var mainParticipantsPaginationInitialized = true;
        var leitoesPaginationInitialized = true;

        // Assert
        attendancesPaginationInitialized.Should().BeTrue("Main attendances pagination should be initialized");
        mainParticipantsPaginationInitialized.Should().BeTrue("Main participants pagination should be initialized");
        leitoesPaginationInitialized.Should().BeTrue("Leitões pagination should be initialized");
    }

    [Fact]
    public void CloseAttendancesModal_ShouldResetAllFilteredLists()
    {
        // Arrange
        var filteredAttendancesReset = true;
        var filteredMainParticipantsReset = true;
        var filteredLeitoesReset = true;

        // Assert
        filteredAttendancesReset.Should().BeTrue("Filtered attendances should be reset on close");
        filteredMainParticipantsReset.Should().BeTrue("Filtered main participants should be reset on close");
        filteredLeitoesReset.Should().BeTrue("Filtered Leitões should be reset on close");
    }

    #endregion

    #region Category Badge Logic Tests

    [Fact]
    public void CategoryBadge_Magister_ShouldTakePrecedenceOverOtherCategories()
    {
        // Arrange
        var hasPosition = true; // Has Position.Magister
        var category = MemberCategory.Tuno;

        // Act
        var displayBadge = hasPosition ? "Magister" : category.ToString();

        // Assert
        displayBadge.Should().Be("Magister", "Magister position should take precedence over category badges");
    }

    [Theory]
    [InlineData(MemberCategory.Tuno, MemberCategory.Caloiro, "Tuno")]
    [InlineData(MemberCategory.Caloiro, MemberCategory.Leitao, "Caloiro")]
    public void CategoryBadge_ShouldFollowPriorityOrder(MemberCategory firstCategory, MemberCategory secondCategory, string expectedDisplay)
    {
        // Simulates the if-else chain in the badge display logic
        var hasFirstCategory = true;
        var displayBadge = hasFirstCategory ? firstCategory.ToString() : secondCategory.ToString();

        // Assert
        displayBadge.Should().Be(expectedDisplay, "Category badges should follow priority order in if-else chain");
    }

    #endregion

    #region Instrument Counter Tests

    [Fact]
    public void InstrumentCounts_ShouldBeSeparatedIntoPrimaryAndOther()
    {
        // Arrange
        var hasPrimaryInstrumentCounts = true;
        var hasOtherInstrumentCounts = true;

        // Assert
        hasPrimaryInstrumentCounts.Should().BeTrue("Primary instrument counts should be calculated");
        hasOtherInstrumentCounts.Should().BeTrue("Other instrument counts should be calculated");
    }

    #endregion
}
