using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the EventCard component to ensure event cards display correctly
/// </summary>
public class EventCardTests : TestContext
{
    private readonly Fixture _fixture;

    public EventCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void EventCard_RendersEventName()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 5));

        // Assert
        cut.Markup.Should().Contain("Test Event", "card should display event name");
    }

    [Fact]
    public void EventCard_RendersEventDate()
    {
        // Arrange
        var eventDate = new DateTime(2025, 12, 25);
        var eventEntity = Event.Create("Christmas Event", eventDate, "Concert Hall", EventType.Festival);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("25 Dec 2025", "card should display formatted event date");
    }

    [Fact]
    public void EventCard_RendersLocation_WhenProvided()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Concert Hall", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("Concert Hall", "card should display location");
        cut.Markup.Should().Contain("bi-geo-alt", "location should have icon");
    }

    [Fact]
    public void EventCard_DoesNotRenderDescription_DescriptionMovedToDetailsModal()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao, "This is a great event!");

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert - Description should NOT be rendered in the card (moved to details modal)
        cut.Markup.Should().NotContain("This is a great event!", "card should not display description - it's in the details modal");
    }

    [Fact]
    public void EventCard_RendersEnrollmentCount()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 15));

        // Assert
        cut.Markup.Should().Contain("15", "card should display enrollment count");
        cut.Markup.Should().Contain("bi-people", "enrollment count should have people icon");
    }

    [Fact]
    public void EventCard_ShowsEnrollButton_WhenUserNotEnrolled()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.UserEnrollment, (Enrollment?)null)
            .Add(p => p.IsPastEvent, false)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("bi-plus-circle-fill", "should show enroll button");
        cut.Markup.Should().Contain("btn-purple", "enroll button should have purple style");
    }

    [Fact]
    public void EventCard_ShowsEnrolledState_WhenUserIsEnrolled()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);
        var enrollment = Enrollment.Create("user123", eventEntity.Id);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.UserEnrollment, enrollment)
            .Add(p => p.IsPastEvent, false)
            .Add(p => p.EnrollmentCount, 1));

        // Assert
        cut.Markup.Should().Contain("bi-check-circle-fill", "should show enrolled check icon");
        cut.Markup.Should().Contain("btn-purple", "enrolled button should have purple style");
    }

    [Fact]
    public void EventCard_ShowsEditAndRemoveButtons_WhenUserIsEnrolled()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);
        var enrollment = Enrollment.Create("user123", eventEntity.Id);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.UserEnrollment, enrollment)
            .Add(p => p.IsPastEvent, false)
            .Add(p => p.EnrollmentCount, 1));

        // Assert
        cut.Markup.Should().Contain("bi-pencil-fill", "should show edit enrollment button");
        cut.Markup.Should().Contain("bi-x-circle-fill", "should show remove enrollment button");
        cut.Markup.Should().Contain("btn-danger", "remove button should have danger style");
    }

    [Fact]
    public void EventCard_DoesNotShowEnrollmentSection_WhenEventIsPast()
    {
        // Arrange
        var eventEntity = Event.Create("Past Event", DateTime.Now.AddDays(-7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.IsPastEvent, true)
            .Add(p => p.EnrollmentCount, 5));

        // Assert - Past events should show button section (details, enrollments, repertoire), but not enrollment action buttons
        cut.Markup.Should().Contain("enrollment-section", "past events should show view buttons section");
        cut.Markup.Should().NotContain("Inscrever", "past events should not show enroll button");
    }

    [Fact]
    public void EventCard_ShowsAdminButtons_WhenUserIsAdmin()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("admin-overlay", "admin overlay should appear");
        cut.Markup.Should().Contain("bi-pencil", "edit button should appear");
        cut.Markup.Should().Contain("bi-trash", "delete button should appear");
    }

    [Fact]
    public void EventCard_DoesNotShowAdminButtons_WhenUserIsNotAdmin()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().NotContain("admin-overlay", "admin overlay should not appear for non-admin");
    }

    [Fact]
    public void EventCard_ShowsWatchRepertoireButton()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.IsPastEvent, false)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("bi-music-note-list", "should show watch repertoire button");
        cut.Markup.Should().Contain("btn-outline-primary", "repertoire button should have primary outline style");
    }

    [Fact]
    public void EventCard_HasCardClasses()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("card", "should have card class");
        cut.Markup.Should().Contain("event-card", "should have event-card class");
    }

    [Fact]
    public void EventCard_RendersPlaceholderImage_WhenNoImageProvided()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("event-image-placeholder", "should show placeholder");
        cut.Markup.Should().Contain("bi-calendar-event", "placeholder should have calendar icon");
    }

    [Fact]
    public void EventCard_InvokesOnEnroll_WhenEnrollButtonClicked()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Atuacao);
        bool callbackInvoked = false;

        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.UserEnrollment, (Enrollment?)null)
            .Add(p => p.IsPastEvent, false)
            .Add(p => p.EnrollmentCount, 0)
            .Add(p => p.OnEnroll, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var enrollButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-purple"));
        enrollButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEnroll callback should be invoked");
    }

    [Theory]
    [InlineData(EventType.Atuacao)]
    [InlineData(EventType.Festival)]
    [InlineData(EventType.Convivio)]
    public void EventCard_RendersCorrectly_ForDifferentEventTypes(EventType eventType)
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Location", eventType);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("Test Event", $"event type {eventType} should display name");
        cut.Markup.Should().Contain("card", $"event type {eventType} should have card class");
    }

    [Fact]
    public void EventCard_ShowsHojeBadge_WhenEventIsToday()
    {
        // Arrange
        var today = DateTime.Today;
        var eventEntity = Event.Create("Today Event", today, "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("HOJE", "should display HOJE badge for today's event");
        cut.Markup.Should().Contain("event-today-badge", "should have event-today-badge class");
    }

    [Fact]
    public void EventCard_DoesNotShowHojeBadge_WhenEventIsTomorrow()
    {
        // Arrange
        var tomorrow = DateTime.Today.AddDays(1);
        var eventEntity = Event.Create("Tomorrow Event", tomorrow, "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().NotContain("HOJE", "should not display HOJE badge for tomorrow's event");
        cut.Markup.Should().NotContain("event-today-badge", "should not have event-today-badge class");
    }

    [Fact]
    public void EventCard_DoesNotShowHojeBadge_WhenEventWasYesterday()
    {
        // Arrange
        var yesterday = DateTime.Today.AddDays(-1);
        var eventEntity = Event.Create("Yesterday Event", yesterday, "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.IsPastEvent, true)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().NotContain("HOJE", "should not display HOJE badge for yesterday's event");
        cut.Markup.Should().NotContain("event-today-badge", "should not have event-today-badge class");
    }

    [Fact]
    public void EventCard_DisplaysTime_WhenEventHasTimeComponent()
    {
        // Arrange
        var eventDate = new DateTime(2025, 12, 25, 17, 0, 0); // 5 PM
        var eventEntity = Event.Create("Evening Event", eventDate, "Concert Hall", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("25 Dec 2025", "card should display event date");
        cut.Markup.Should().Contain("17:00h", "card should display time when event has time component");
    }

    [Fact]
    public void EventCard_DoesNotDisplayTime_WhenEventIsAtMidnight()
    {
        // Arrange
        var eventDate = new DateTime(2025, 12, 25, 0, 0, 0); // Midnight (all-day event)
        var eventEntity = Event.Create("All Day Event", eventDate, "Location", EventType.Atuacao);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("25 Dec 2025", "card should display event date");
        cut.Markup.Should().NotContain("00:00h", "card should not display time for midnight (all-day events)");
    }

    [Fact]
    public void EventCard_DisplaysDateRange_WithoutTime()
    {
        // Arrange
        var startDate = new DateTime(2025, 12, 20, 0, 0, 0);
        var endDate = new DateTime(2025, 12, 25, 0, 0, 0);
        var eventEntity = Event.Create("Multi-day Event", startDate, "Location", EventType.Festival);
        eventEntity.SetEndDate(endDate);

        // Act
        var cut = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eventEntity)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cut.Markup.Should().Contain("20 Dec", "card should display start date");
        cut.Markup.Should().Contain("25 Dec 2025", "card should display end date");
        cut.Markup.Should().NotContain("00:00h", "card should not display time for date ranges");
    }

    [Fact]
    public void EventCard_DisplaysTime_WithDifferentHours()
    {
        // Arrange
        var morningEvent = Event.Create("Morning Event", new DateTime(2025, 12, 25, 9, 30, 0), "Location", EventType.Atuacao);
        var eveningEvent = Event.Create("Evening Event", new DateTime(2025, 12, 25, 19, 0, 0), "Location", EventType.Atuacao);

        // Act
        var cutMorning = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, morningEvent)
            .Add(p => p.EnrollmentCount, 0));

        var cutEvening = RenderComponent<EventCard>(parameters => parameters
            .Add(p => p.Event, eveningEvent)
            .Add(p => p.EnrollmentCount, 0));

        // Assert
        cutMorning.Markup.Should().Contain("09:30h", "morning event should display 09:30h");
        cutEvening.Markup.Should().Contain("19:00h", "evening event should display 19:00h");
    }
}
