using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the MeetingCard component to ensure meeting cards display correctly with all UI/UX improvements
/// </summary>
public class MeetingCardTests : TestContext
{
    [Fact]
    public void MeetingCard_RendersMeetingTitle()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "CONVOCATÓRIA",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("CONVOCATÓRIA", "card should display meeting title");
    }

    [Fact]
    public void MeetingCard_RendersMeetingDate()
    {
        // Arrange
        var meetingDate = new DateTime(2025, 11, 25, 15, 30, 0);
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = meetingDate,
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("25 Nov 2025", "card should display formatted meeting date");
        cut.Markup.Should().Contain("15:30", "card should display formatted meeting time");
    }

    [Fact]
    public void MeetingCard_RendersDayOfWeek()
    {
        // Arrange
        var tuesday = new DateTime(2025, 11, 25); // This is a Tuesday
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = tuesday,
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("(Terça)", "card should display day of week in parentheses");
    }

    [Fact]
    public void MeetingCard_RendersLocation_WhenProvided()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede Principal",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("Sede Principal", "card should display location");
        cut.Markup.Should().Contain("bi-geo-alt", "location should have icon");
    }

    [Fact]
    public void MeetingCard_DisplaysFullMeetingTypeInHeader()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("Assembleia Geral Ordinária", "header should show full meeting type");
        cut.Markup.Should().NotContain("AGO", "header should not show abbreviation");
    }

    [Theory]
    [InlineData(MeetingType.AssembleiaGeralOrdinaria, "Assembleia Geral Ordinária")]
    [InlineData(MeetingType.AssembleiaGeralExtraordinaria, "Assembleia Geral Extraordinária")]
    [InlineData(MeetingType.ConselhoVeteranos, "Conselho de Veteranos")]
    public void MeetingCard_DisplaysCorrectTypeText_ForAllMeetingTypes(MeetingType type, string expectedText)
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = type,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"should show {expectedText} for type {type}");
    }

    [Fact]
    public void MeetingCard_ShowsAgendadaStatus_ForUpcomingMeeting()
    {
        // Arrange
        var futureMeeting = new Meeting
        {
            Title = "Future Meeting",
            Date = DateTime.Today.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, futureMeeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("Agendada", "upcoming meeting should show 'Agendada' status");
        cut.Markup.Should().Contain("meeting-status-upcoming", "upcoming meeting should have upcoming status class");
    }

    [Fact]
    public void MeetingCard_ShowsRealizadaStatus_ForPastMeeting()
    {
        // Arrange
        var pastMeeting = new Meeting
        {
            Title = "Past Meeting",
            Date = DateTime.Today.AddDays(-7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, pastMeeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("Realizada", "past meeting should show 'Realizada' status");
        cut.Markup.Should().Contain("meeting-status-past", "past meeting should have past status class");
    }

    [Fact]
    public void MeetingCard_ShowsVerDetalhesButton()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("Ver Detalhes", "should show 'Ver Detalhes' button");
        cut.Markup.Should().Contain("bi-eye-fill", "details button should have eye icon");
        var detailsButtons = cut.FindAll("button").Where(b => b.TextContent.Contains("Ver Detalhes"));
        detailsButtons.Should().NotBeEmpty("details button element should exist");
    }

    [Fact]
    public void MeetingCard_ShowsAdminButtons_WhenUserIsAdmin()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, true));

        // Assert
        cut.Markup.Should().Contain("bi-pencil", "edit button should appear");
        cut.Markup.Should().Contain("bi-trash", "delete button should appear");
        cut.Markup.Should().Contain("music-icon-edit", "edit icon should have music-icon-edit class");
        cut.Markup.Should().Contain("music-icon-delete", "delete icon should have music-icon-delete class");
        var editButtons = cut.FindAll("button").Where(b => b.ClassList.Contains("music-btn-edit"));
        editButtons.Should().NotBeEmpty("edit button should appear");
        var deleteButtons = cut.FindAll("button").Where(b => b.ClassList.Contains("music-btn-delete"));
        deleteButtons.Should().NotBeEmpty("delete button should appear");
    }

    [Fact]
    public void MeetingCard_DoesNotShowAdminButtons_WhenUserIsNotAdmin()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert - Check that admin buttons don't appear for non-admin
        var editButtons = cut.FindAll("button").Where(b => b.ClassList.Contains("music-btn-edit"));
        editButtons.Should().BeEmpty("edit button should not appear for non-admin");
        var deleteButtons = cut.FindAll("button").Where(b => b.ClassList.Contains("music-btn-delete"));
        deleteButtons.Should().BeEmpty("delete button should not appear for non-admin");
    }

    [Fact]
    public void MeetingCard_ShowsEmailButton_WhenUserIsAdmin()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, true));

        // Assert
        cut.Markup.Should().Contain("bi-envelope-fill", "email button should have envelope icon");
        cut.Markup.Should().Contain("Notificar", "email button should show 'Notificar' text");
        var emailButtons = cut.FindAll("button").Where(b => b.TextContent.Contains("Notificar"));
        emailButtons.Should().NotBeEmpty("email button element should exist for admin");
    }

    [Fact]
    public void MeetingCard_DoesNotShowEmailButton_WhenUserIsNotAdmin()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().NotContain("Notificar", "email button should not appear for non-admin");
        var emailButtons = cut.FindAll("button").Where(b => b.TextContent.Contains("Notificar"));
        emailButtons.Should().BeEmpty("no email button elements should exist for non-admin");
    }

    [Fact]
    public void MeetingCard_ShowsHojeBadge_WhenMeetingIsToday()
    {
        // Arrange
        var todayMeeting = new Meeting
        {
            Title = "Today Meeting",
            Date = DateTime.Today.AddHours(15),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, todayMeeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("Hoje", "should display 'Hoje' badge for today's meeting");
        cut.Markup.Should().Contain("meeting-today-badge", "should have meeting-today-badge class");
    }

    [Fact]
    public void MeetingCard_DoesNotShowHojeBadge_WhenMeetingIsTomorrow()
    {
        // Arrange
        var tomorrowMeeting = new Meeting
        {
            Title = "Tomorrow Meeting",
            Date = DateTime.Today.AddDays(1),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, tomorrowMeeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().NotContain("Hoje", "should not display 'Hoje' badge for tomorrow's meeting");
    }

    [Fact]
    public void MeetingCard_HasStatusBadge()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("meeting-status-badge", "should have meeting-status-badge class");
    }

    [Fact]
    public void MeetingCard_HasCardClasses()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("card", "should have card class");
        cut.Markup.Should().Contain("meeting-card", "should have meeting-card class");
    }

    [Fact]
    public void MeetingCard_InvokesOnViewDetails_WhenDetailsButtonClicked()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };
        bool callbackInvoked = false;

        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.OnViewDetails, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var detailsButton = cut.FindAll("button").First(b => b.TextContent.Contains("Ver Detalhes"));
        detailsButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnViewDetails callback should be invoked");
    }

    [Fact]
    public void MeetingCard_InvokesOnEdit_WhenEditButtonClicked()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };
        bool callbackInvoked = false;

        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.OnEdit, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var editButton = cut.FindAll("button").First(b => b.ClassList.Contains("music-btn-edit"));
        editButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEdit callback should be invoked");
    }

    [Fact]
    public void MeetingCard_InvokesOnDelete_WhenDeleteButtonClicked()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };
        bool callbackInvoked = false;

        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.OnDelete, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var deleteButton = cut.FindAll("button").First(b => b.ClassList.Contains("music-btn-delete"));
        deleteButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnDelete callback should be invoked");
    }

    [Fact]
    public void MeetingCard_InvokesOnSendEmail_WhenEmailButtonClicked()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };
        bool callbackInvoked = false;

        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.OnSendEmail, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var emailButton = cut.FindAll("button").First(b => b.TextContent.Contains("Notificar"));
        emailButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnSendEmail callback should be invoked");
    }

    [Fact]
    public void MeetingCard_HasFooterWithCenteredButtons()
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, true));

        // Assert
        var footerDivs = cut.FindAll("div").Where(d => d.ClassList.Contains("justify-content-center"));
        footerDivs.Should().NotBeEmpty("should have centered container for buttons");
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "Segunda")]
    [InlineData(DayOfWeek.Tuesday, "Terça")]
    [InlineData(DayOfWeek.Wednesday, "Quarta")]
    [InlineData(DayOfWeek.Thursday, "Quinta")]
    [InlineData(DayOfWeek.Friday, "Sexta")]
    [InlineData(DayOfWeek.Saturday, "Sábado")]
    [InlineData(DayOfWeek.Sunday, "Domingo")]
    public void MeetingCard_DisplaysCorrectPortugueseDayOfWeek(DayOfWeek day, string expectedPortuguese)
    {
        // Arrange
        var date = GetNextDateForDayOfWeek(day);
        var meeting = new Meeting
        {
            Title = "Test Meeting",
            Date = date,
            Location = "Sede",
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain($"({expectedPortuguese})", $"should display {expectedPortuguese} for {day}");
    }

    private DateTime GetNextDateForDayOfWeek(DayOfWeek targetDay)
    {
        var today = DateTime.Today;
        var daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0) daysUntilTarget = 7; // If today is the target, get next week
        return today.AddDays(daysUntilTarget);
    }

    [Theory]
    [InlineData(MeetingType.AssembleiaGeralOrdinaria, "bg-primary")]
    [InlineData(MeetingType.AssembleiaGeralExtraordinaria, "bg-danger")]
    [InlineData(MeetingType.ConselhoVeteranos, "bg-warning text-dark")]
    public void MeetingCard_DisplaysColoredBadgeForMeetingType(MeetingType type, string expectedBadgeClass)
    {
        // Arrange
        var meeting = new Meeting
        {
            Title = "CONVOCATÓRIA",
            Date = DateTime.Now.AddDays(7),
            Location = "Sede",
            Type = type,
            Statement = "Test statement"
        };

        // Act
        var cut = RenderComponent<MeetingCard>(parameters => parameters
            .Add(p => p.Meeting, meeting)
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().Contain("badge", "should have Bootstrap badge class");
        cut.Markup.Should().Contain(expectedBadgeClass, $"should have {expectedBadgeClass} for meeting type {type}");
    }
}
