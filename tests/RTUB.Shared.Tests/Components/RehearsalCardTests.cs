using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the RehearsalCard component to ensure rehearsal cards display correctly
/// </summary>
public class RehearsalCardTests : TestContext
{
    private readonly Fixture _fixture;

    public RehearsalCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void RehearsalCard_RendersRehearsalDate()
    {
        // Arrange
        var rehearsalDate = new DateTime(2025, 12, 25);
        var rehearsal = Rehearsal.Create(rehearsalDate, "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("25 Dec 2025", "card should display formatted rehearsal date");
    }

    [Fact]
    public void RehearsalCard_RendersLocation()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Main Hall");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("Main Hall", "card should display location");
        cut.Markup.Should().Contain("bi-geo-alt", "location should have icon");
    }

    [Fact]
    public void RehearsalCard_RendersTheme_WhenProvided()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room", "Christmas Songs");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("Christmas Songs", "card should display theme");
        cut.Markup.Should().Contain("Tema:", "theme label should be present");
    }

    [Fact]
    public void RehearsalCard_DoesNotRenderTheme_WhenNotProvided()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().NotContain("Tema:", "theme section should not appear when no theme");
    }

    [Fact]
    public void RehearsalCard_ShowsCanceledStatus_WhenRehearsalIsCanceled()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        rehearsal.Cancel("Test cancellation");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("CANCELADO", "should display canceled status");
        cut.Markup.Should().Contain("badge bg-danger fs-6", "canceled status should use danger badge styling");
        cut.Markup.Should().Contain("bi-x-circle-fill", "canceled status should have X icon");
    }

    [Fact]
    public void RehearsalCard_ShowsRehearsalInfo_WhenNotCanceled()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("rehearsal-info", "should display rehearsal info section");
        cut.Markup.Should().NotContain("Cancelado", "should not display cancelled status");
    }

    [Fact]
    public void RehearsalCard_RendersAttendanceCount()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 12));

        // Assert
        cut.Markup.Should().Contain("12", "card should display attendance count");
        cut.Markup.Should().Contain("bi-people", "attendance count should have people icon");
    }

    [Fact]
    public void RehearsalCard_ShowsToggleButtons_WhenUserHasNotMarkedAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, (RehearsalAttendance?)null)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 0));

        // Assert - New toggle button design: green + (add) and red X (not going) when no attendance
        cut.Markup.Should().Contain("bi-plus-circle-fill", "should show green add button");
        cut.Markup.Should().Contain("bi-x-circle-fill", "should show not going button");
        cut.Markup.Should().Contain("btn-add-attendance", "add button should have green add style");
        cut.Markup.Should().Contain("btn-not-attending", "not going button should have red style");
    }

    [Fact]
    public void RehearsalCard_ShowsPendingStatus_WhenUserAttendanceNotApproved()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert - New toggle button design: pending status shown as yellow clock with btn-selected
        cut.Markup.Should().Contain("bi-clock-fill", "should show pending clock icon");
        cut.Markup.Should().Contain("btn-pending", "pending button should have pending style");
        cut.Markup.Should().Contain("btn-selected", "pending button should be selected");
    }

    [Fact]
    public void RehearsalCard_ShowsToggleButtons_WhenUserHasAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert - New toggle button design: clock (edit going) and X (not going)
        cut.Markup.Should().Contain("bi-clock-fill", "should show pending clock icon");
        cut.Markup.Should().Contain("bi-x-circle-fill", "should show not going button");
        cut.Markup.Should().Contain("btn-pending", "pending button should have pending style");
        cut.Markup.Should().Contain("btn-not-attending", "not-attending button should have red style");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowAttendanceSection_WhenRehearsalIsPast()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsPastRehearsal, true)
            .Add(p => p.AttendanceCount, 5));

        // Assert
        cut.Markup.Should().NotContain("enrollment-section", "attendance section should not appear for past rehearsals");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowAttendanceSection_WhenRehearsalIsCanceled()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        rehearsal.Cancel("Test cancellation");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 5));

        // Assert
        cut.Markup.Should().NotContain("enrollment-section", "attendance section should not appear for canceled rehearsals");
    }

    [Fact]
    public void RehearsalCard_ShowsAdminButtons_WhenUserIsAdmin()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("rehearsal-admin-overlay", "admin overlay should appear");
        cut.Markup.Should().Contain("bi-pencil", "edit button should appear");
        cut.Markup.Should().Contain("bi-trash", "delete button should appear");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowAdminButtons_WhenUserIsNotAdmin()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().NotContain("rehearsal-admin-overlay", "admin overlay should not appear for non-admin");
    }

    [Fact]
    public void RehearsalCard_ShowsTodayBadge_WhenRehearsalIsToday()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Today, "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("HOJE", "should display 'HOJE' badge for today's rehearsal");
        cut.Markup.Should().Contain("date-badge-hoje", "should have date-badge-hoje class");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowTodayBadge_WhenRehearsalIsNotToday()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Today.AddDays(1), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().NotContain("Hoje", "should not display 'Hoje' badge when not today");
    }

    [Fact]
    public void RehearsalCard_HasCardClasses()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("card", "should have card class");
        cut.Markup.Should().Contain("rehearsal-card", "should have rehearsal-card class");
    }

    [Fact]
    public void RehearsalCard_InvokesOnAttendPending_WhenPendingButtonClicked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        bool callbackInvoked = false;

        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, (RehearsalAttendance?)null)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 0)
            .Add(p => p.OnAttendPending, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act - Now uses green add button instead of yellow pending
        var addButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-add-attendance"));
        addButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnAttendPending callback should be invoked");
    }

    [Fact]
    public void RehearsalCard_ViewDetailsButton_HasTextLabel()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 5));

        // Assert
        cut.Markup.Should().Contain("Ver", "View Details button should have 'Ver' text label");
        cut.Markup.Should().Contain("bi-eye-fill", "View Details button should have eye icon");
        cut.Markup.Should().Contain("btn-outline-primary", "View Details button should have primary outline style");
    }

    [Fact]
    public void RehearsalCard_ViewAttendancesButton_HasAttendanceCount()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 8));

        // Assert
        cut.Markup.Should().Contain("8", "View Attendances button should display attendance count");
        cut.Markup.Should().Contain("bi-people", "View Attendances button should have people icon");
        cut.Markup.Should().Contain("btn-outline-secondary", "View Attendances button should have secondary outline style");
    }

    [Fact]
    public void RehearsalCard_ViewButtons_AreSameSize()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 5));

        // Assert
        var buttons = cut.FindAll("button").Where(b =>
            b.ClassList.Contains("btn-outline-primary") || b.ClassList.Contains("btn-outline-secondary")).ToList();

        buttons.Should().HaveCount(2, "should have two view buttons");
        buttons.All(b => b.ClassList.Contains("gap-1")).Should().BeTrue("both buttons should have gap-1 class for consistent sizing");
    }

    [Fact]
    public void RehearsalCard_InvokesOnViewDetails_WhenViewDetailsButtonClicked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        bool callbackInvoked = false;

        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 5)
            .Add(p => p.OnViewDetails, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var viewDetailsButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-outline-primary"));
        viewDetailsButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnViewDetails callback should be invoked");
    }

    [Fact]
    public void RehearsalCard_InvokesOnViewAttendances_WhenViewAttendancesButtonClicked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        bool callbackInvoked = false;

        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 5)
            .Add(p => p.OnViewAttendances, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var viewAttendancesButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-outline-secondary"));
        viewAttendancesButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnViewAttendances callback should be invoked");
    }

    [Fact]
    public void RehearsalCard_InvokesOnEditAttendance_WhenPendingButtonClickedWithAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        bool callbackInvoked = false;

        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1)
            .Add(p => p.OnEditAttendance, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act - Click the pending button which now triggers edit when user has attendance
        var pendingButton = cut.FindAll("button").First(b =>
            b.ClassList.Contains("btn-pending") && b.ClassList.Contains("btn-selected"));
        pendingButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEditAttendance callback should be invoked when clicking pending button with attendance");
    }

    [Fact]
    public void RehearsalCard_InvokesOnAttendNotGoing_WhenNotGoingButtonClicked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        bool callbackInvoked = false;

        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1)
            .Add(p => p.OnAttendNotGoing, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var notGoingButton = cut.FindAll("button").First(b =>
            b.ClassList.Contains("btn-not-attending") && b.InnerHtml.Contains("bi-x-circle-fill"));
        notGoingButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnAttendNotGoing callback should be invoked");
    }

    #region Status Badge Tests

    [Fact]
    public void RehearsalCard_ShowsVouBadge_WhenUserAttendingAsGoing()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        cut.Markup.Should().Contain("VOU", "should display VOU badge when attending as going");
        cut.Markup.Should().Contain("bg-success", "VOU badge should have success (green) style");
    }

    [Fact]
    public void RehearsalCard_ShowsNaoVouBadge_WhenUserAttendingAsNotGoing()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = false;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        cut.Markup.Should().Contain("NÃO VOU", "should display NÃO VOU badge when attending as not going");
        cut.Markup.Should().Contain("bg-danger", "NÃO VOU badge should have danger (red) style");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowStatusBadge_WhenUserHasNoAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, (RehearsalAttendance?)null)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().NotContain("rehearsal-status-badge", "should not display status badge when no attendance");
    }

    [Fact]
    public void RehearsalCard_ShowsCanceladoBadge_OverridesStatusBadge_WhenRehearsalIsCanceled()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        rehearsal.Cancel("Rehearsal cancelled");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        cut.Markup.Should().Contain("CANCELADO", "should display CANCELADO badge");
        cut.Markup.Should().NotContain("rehearsal-status-badge", "status badge should be hidden when rehearsal is cancelled");
    }

    [Fact]
    public void RehearsalCard_ShowsDateBadge_WhenNoAttendanceAndNotCanceled()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Today, "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, (RehearsalAttendance?)null)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("HOJE", "should display date badge (HOJE) when no attendance");
    }

    #endregion

    #region Button Selection State Tests

    [Fact]
    public void RehearsalCard_YellowPendingButtonSelected_WhenAttendancePending()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;
        attendance.Attended = false;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        var pendingButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-pending"));
        pendingButton.ClassList.Should().Contain("btn-selected", "pending button should be selected when attendance is pending");
    }

    [Fact]
    public void RehearsalCard_RedNotAttendingButtonSelected_WhenNotGoing()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = false;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        var notGoingButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-not-attending"));
        notGoingButton.ClassList.Should().Contain("btn-selected", "not attending button should be selected when user is not going");
    }

    [Fact]
    public void RehearsalCard_GreenApprovedButtonSelected_WhenAttendanceApproved()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;
        attendance.Attended = true;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        var approvedButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-approved"));
        approvedButton.ClassList.Should().Contain("btn-selected", "approved button should be selected when attendance is approved");
    }

    [Fact]
    public void RehearsalCard_SecondaryButtonSmaller_WhenAttendanceMarked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        var notAttendingButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-not-attending"));
        notAttendingButton.ClassList.Should().Contain("btn-secondary-small", "not attending button should be smaller when user has attendance marked");
    }

    [Fact]
    public void RehearsalCard_BothButtonsSameSize_WhenNoAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, (RehearsalAttendance?)null)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        var addButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-add-attendance"));
        var notAttendingButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-not-attending"));
        addButton.ClassList.Should().NotContain("btn-secondary-small", "add button should not be smaller when no attendance");
        notAttendingButton.ClassList.Should().NotContain("btn-secondary-small", "not attending button should not be smaller when no attendance");
    }

    #endregion

    #region Past Rehearsal Tests

    [Fact]
    public void RehearsalCard_ShowsDeleteAndPendingIcon_ForPastRehearsalWithPendingAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;
        attendance.Attended = false;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, true)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        cut.Markup.Should().Contain("bi-clock-fill", "should show pending clock icon for past rehearsal with pending attendance");
        cut.Markup.Should().Contain("bi-trash-fill", "should show delete button for past rehearsal with pending attendance");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowDeleteButton_ForPastRehearsalWithApprovedAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.WillAttend = true;
        attendance.Attended = true;

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, true)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        cut.Markup.Should().NotContain("bi-trash-fill", "should not show delete button for past rehearsal with approved attendance");
    }

    #endregion

    #region Pending Approvals Reminder Tests

    [Fact]
    public void RehearsalCard_ShowsPendingApprovalsReminder_WhenAdminAndPastRehearsalWithPending()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.IsPastRehearsal, true)
            .Add(p => p.HasPendingApprovals, true)
            .Add(p => p.AttendanceCount, 5));

        // Assert - Ver Presenças button should be yellow with clock icon for pending approvals
        cut.Markup.Should().Contain("bi-clock-fill", "should show clock icon on Ver Presenças button");
        cut.Markup.Should().Contain("#f39c12", "Ver Presenças button should have yellow background color");
        cut.Markup.Should().Contain("Tem presenças pendentes para aprovar", "should have tooltip explaining pending approvals");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowPendingApprovalsReminder_WhenNotAdmin()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsPastRehearsal, true)
            .Add(p => p.HasPendingApprovals, true)
            .Add(p => p.AttendanceCount, 5));

        // Assert - Should show regular people icon, not clock, for non-admin
        cut.Markup.Should().Contain("bi-people", "should show people icon for non-admin");
        cut.Markup.Should().NotContain("#f39c12", "Ver Presenças button should not have yellow color for non-admin");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowPendingApprovalsReminder_WhenNotPastRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.HasPendingApprovals, true)
            .Add(p => p.AttendanceCount, 5));

        // Assert - Should show regular people icon for upcoming rehearsals, not clock
        cut.Markup.Should().Contain("bi-people", "should show people icon for upcoming rehearsals");
        cut.Markup.Should().NotContain("#f39c12", "Ver Presenças button should not have yellow color for upcoming rehearsals");
    }

    [Fact]
    public void RehearsalCard_DoesNotShowPendingApprovalsReminder_WhenNoPendingApprovals()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-7), "Music Room");

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.IsPastRehearsal, true)
            .Add(p => p.HasPendingApprovals, false)
            .Add(p => p.AttendanceCount, 5));

        // Assert - Should show regular people icon when no pending approvals
        cut.Markup.Should().Contain("bi-people", "should show people icon when no pending approvals");
        cut.Markup.Should().NotContain("#f39c12", "Ver Presenças button should not have yellow color when no pending approvals");
    }

    #endregion
}
