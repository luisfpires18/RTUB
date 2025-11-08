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
        rehearsal.Cancel();

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.AttendanceCount, 0));

        // Assert
        cut.Markup.Should().Contain("Cancelado", "should display canceled status");
        cut.Markup.Should().Contain("rehearsal-status-badge cancelled", "canceled status should have cancelled badge class");
        cut.Markup.Should().Contain("bi-x-circle", "canceled status should have X icon");
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
    public void RehearsalCard_ShowsMarkAttendanceButton_WhenUserHasNotMarkedAttendance()
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
        cut.Markup.Should().Contain("bi-plus-circle-fill", "should show mark attendance button");
        cut.Markup.Should().Contain("btn-purple", "mark attendance button should have purple style");
    }

    [Fact]
    public void RehearsalCard_ShowsAttendedStatus_WhenUserAttendanceIsMarked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        var attendance = RehearsalAttendance.Create(1, "user123");
        attendance.MarkAttendance(true);

        // Act
        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, attendance)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 1));

        // Assert
        cut.Markup.Should().Contain("bi-check-circle-fill", "should show attended check icon");
        cut.Markup.Should().Contain("btn-success", "attended button should have success style");
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

        // Assert
        cut.Markup.Should().Contain("bi-clock-fill", "should show pending clock icon");
        cut.Markup.Should().Contain("btn-warning", "pending button should have warning style");
    }

    [Fact]
    public void RehearsalCard_ShowsEditAndRemoveButtons_WhenUserHasAttendance()
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

        // Assert
        cut.Markup.Should().Contain("bi-pencil-fill", "should show edit attendance button");
        cut.Markup.Should().Contain("bi-x-circle-fill", "should show remove attendance button");
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
        rehearsal.Cancel();

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
        cut.Markup.Should().Contain("Hoje", "should display 'Hoje' badge for today's rehearsal");
        cut.Markup.Should().Contain("rehearsal-today-badge", "should have today badge class");
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
    public void RehearsalCard_InvokesOnMarkAttendance_WhenMarkAttendanceButtonClicked()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Music Room");
        bool callbackInvoked = false;

        var cut = RenderComponent<RehearsalCard>(parameters => parameters
            .Add(p => p.Rehearsal, rehearsal)
            .Add(p => p.UserAttendance, (RehearsalAttendance?)null)
            .Add(p => p.IsPastRehearsal, false)
            .Add(p => p.AttendanceCount, 0)
            .Add(p => p.OnMarkAttendance, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var markButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-purple"));
        markButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnMarkAttendance callback should be invoked");
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
    public void RehearsalCard_InvokesOnEditAttendance_WhenEditButtonClicked()
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

        // Act
        var editButton = cut.FindAll("button").First(b => 
            b.ClassList.Contains("btn-light") && b.InnerHtml.Contains("bi-pencil-fill"));
        editButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEditAttendance callback should be invoked");
    }

    [Fact]
    public void RehearsalCard_InvokesOnRemoveAttendance_WhenRemoveButtonClicked()
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
            .Add(p => p.OnRemoveAttendance, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var removeButton = cut.FindAll("button").First(b => 
            b.ClassList.Contains("btn-danger") && b.InnerHtml.Contains("bi-x-circle-fill"));
        removeButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnRemoveAttendance callback should be invoked");
    }
}
