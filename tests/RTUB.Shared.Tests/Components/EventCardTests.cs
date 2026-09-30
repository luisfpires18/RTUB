using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// EventCard behavior: content, the member's Vou / Não vou answer, secondary actions and the
/// management menu (docs/design/RTUB_UI_REFACTOR.md section 25). Queries by accessible names
/// and ARIA state rather than CSS classes.
/// </summary>
public class EventCardTests : BunitContext
{
    private static Event Upcoming(string name = "Test Event", string location = "Location", EventType type = EventType.Atuacao) =>
        Event.Create(name, DateTime.Now.AddDays(7), location, type);

    private static Event Past(string name = "Past Event", EventType type = EventType.Atuacao) =>
        Event.Create(name, DateTime.Now.AddDays(-7), "Location", type);

    private static Enrollment Answer(Event e, bool willAttend)
    {
        var enrollment = Enrollment.Create("user123", e.Id);
        enrollment.WillAttend = willAttend;
        return enrollment;
    }

    private IRenderedComponent<EventCard> RenderCard(Event e, Action<ComponentParameterCollectionBuilder<EventCard>>? more = null) =>
        Render<EventCard>(parameters =>
        {
            parameters.Add(p => p.Event, e);
            more?.Invoke(parameters);
        });

    private static IElement Button(IRenderedComponent<EventCard> cut, string name) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == name || b.GetAttribute("aria-label") == name);

    private static bool HasButton(IRenderedComponent<EventCard> cut, string name) =>
        cut.FindAll("button").Any(b => b.TextContent.Trim() == name || b.GetAttribute("aria-label") == name);

    private static List<string> MenuItems(IRenderedComponent<EventCard> cut) =>
        cut.FindAll(".dropdown-menu button").Select(b => b.TextContent.Trim()).ToList();

    #region Content

    [Fact]
    public void EventCard_IsAnArticleNamedByItsTitleHeading()
    {
        var cut = RenderCard(Upcoming("Serenata"));

        var article = cut.Find("article");
        var heading = cut.Find("h3");
        heading.TextContent.Should().Contain("Serenata");
        article.GetAttribute("aria-labelledby").Should().Be(heading.Id);
    }

    [Fact]
    public void EventCard_RendersEventDate()
    {
        var cut = RenderCard(Event.Create("Christmas Event", new DateTime(2025, 12, 25), "Concert Hall", EventType.Festival));

        cut.Markup.Should().Contain("25 Dec 2025");
        cut.Find("time").GetAttribute("datetime").Should().Be("2025-12-25");
    }

    [Fact]
    public void EventCard_RendersLocation_WhenProvided()
    {
        var cut = RenderCard(Upcoming(location: "Concert Hall"));

        cut.Markup.Should().Contain("Concert Hall");
        cut.Markup.Should().Contain("bi-geo-alt");
    }

    [Fact]
    public void EventCard_DoesNotRenderDescription_DescriptionMovedToDetailsModal()
    {
        var e = Upcoming();
        e.Description = "This is a great event!";

        RenderCard(e).Markup.Should().NotContain("This is a great event!");
    }

    [Theory]
    [InlineData(EventType.Atuacao)]
    [InlineData(EventType.Festival)]
    [InlineData(EventType.Convivio)]
    public void EventCard_RendersCorrectly_ForDifferentEventTypes(EventType eventType)
    {
        var cut = RenderCard(Upcoming(type: eventType));

        cut.Find("h3").TextContent.Should().Contain("Test Event");
    }

    [Fact]
    public void EventCard_ShowsHojeBadge_WhenEventIsToday()
    {
        var cut = RenderCard(Event.Create("Today", DateTime.Today.AddHours(20), "Location", EventType.Atuacao));

        cut.Markup.Should().Contain("HOJE");
    }

    [Fact]
    public void EventCard_DoesNotShowHojeBadge_WhenEventIsTomorrow()
    {
        RenderCard(Event.Create("Tomorrow", DateTime.Today.AddDays(1), "Location", EventType.Atuacao))
            .Markup.Should().NotContain("HOJE");
    }

    [Fact]
    public void EventCard_DisplaysTime_WhenEventHasTimeComponent()
    {
        var cut = RenderCard(Event.Create("Evening", new DateTime(2025, 12, 25, 17, 0, 0), "Location", EventType.Atuacao));

        cut.Markup.Should().Contain("25 Dec 2025").And.Contain("17:00h");
    }

    [Fact]
    public void EventCard_DoesNotDisplayTime_WhenEventIsAtMidnight()
    {
        RenderCard(Event.Create("All day", new DateTime(2025, 12, 25), "Location", EventType.Atuacao))
            .Markup.Should().NotContain("00:00h");
    }

    [Fact]
    public void EventCard_DisplaysDateRange_WithoutTime()
    {
        var e = Event.Create("Festival", new DateTime(2025, 12, 20, 10, 0, 0), "Location", EventType.Festival);
        e.EndDate = new DateTime(2025, 12, 25);

        var cut = RenderCard(e);

        cut.Markup.Should().Contain("20 Dec").And.Contain("25 Dec 2025").And.NotContain("10:00h");
    }

    [Fact]
    public void EventCard_ShowsImage_WhenProvided_AndDropsItWhenItFails()
    {
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.ImageUrl, "/img/event.webp"));

        var img = cut.Find("img");
        img.GetAttribute("alt").Should().Be("Test Event");

        img.TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        cut.FindAll("img").Should().BeEmpty("a broken image leaves the card without a media area");
    }

    [Fact]
    public void EventCard_HasNoPlaceholderArt_WhenNoImage()
    {
        var cut = RenderCard(Upcoming());

        cut.FindAll("img").Should().BeEmpty();
        cut.Markup.Should().NotContain("bi-calendar-event");
    }

    #endregion

    #region Member answer (Vou / Não vou)

    [Fact]
    public void EventCard_ShowsLabelledAnswerToggle_WhenNotAnswered()
    {
        var cut = RenderCard(Upcoming());

        cut.Find("[role=group]").GetAttribute("aria-label").Should().Contain("Test Event");
        Button(cut, "Vou").GetAttribute("aria-pressed").Should().Be("false");
        Button(cut, "Não vou").GetAttribute("aria-pressed").Should().Be("false");
        Button(cut, "Vou").GetAttribute("title").Should().Be("Vou participar");
    }

    [Fact]
    public void EventCard_MarksVouPressed_WhenEnrolledAsGoing()
    {
        var e = Upcoming();
        var cut = RenderCard(e, p => p.Add(x => x.UserEnrollment, Answer(e, true)));

        Button(cut, "Vou").GetAttribute("aria-pressed").Should().Be("true");
        Button(cut, "Vou").GetAttribute("title").Should().Be("Editar inscrição");
        Button(cut, "Não vou").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void EventCard_MarksNaoVouPressed_WhenEnrolledAsNotGoing()
    {
        var e = Upcoming();
        var cut = RenderCard(e, p => p.Add(x => x.UserEnrollment, Answer(e, false)));

        Button(cut, "Não vou").GetAttribute("aria-pressed").Should().Be("true");
        Button(cut, "Não vou").GetAttribute("title").Should().Be("Editar impossibilidade");
        Button(cut, "Vou").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void EventCard_InvokesOnEnrollGoing_WhenVouClicked()
    {
        var invoked = false;
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.OnEnrollGoing, EventCallback.Factory.Create(this, () => invoked = true)));

        Button(cut, "Vou").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void EventCard_InvokesOnEnrollNotGoing_WhenNaoVouClicked()
    {
        var invoked = false;
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.OnEnrollNotGoing, EventCallback.Factory.Create(this, () => invoked = true)));

        Button(cut, "Não vou").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void EventCard_HasNoAnswerToggle_WhenEventIsPast()
    {
        var cut = RenderCard(Past(), p => p.Add(x => x.IsPastEvent, true));

        cut.FindAll("[role=group]").Should().BeEmpty();
        HasButton(cut, "Vou").Should().BeFalse();
    }

    [Fact]
    public void EventCard_ShowsFui_AndRemoveEnrollment_OnPastEvent_WhenUserWillAttend()
    {
        var e = Past();
        var removed = false;
        var cut = RenderCard(e, p => p
            .Add(x => x.UserEnrollment, Answer(e, true))
            .Add(x => x.IsPastEvent, true)
            .Add(x => x.OnRemoveEnrollment, EventCallback.Factory.Create(this, () => removed = true)));

        cut.Find(".item-status").TextContent.Should().Contain("Fui");
        var remove = Button(cut, "Remover inscrição");
        remove.GetAttribute("title").Should().Be("Remover Inscrição");
        remove.Click();
        removed.Should().BeTrue();
    }

    [Fact]
    public void EventCard_ShowsNaoFui_AndNoRemove_OnPastEvent_WhenUserWillNotAttend()
    {
        var e = Past();
        var cut = RenderCard(e, p => p.Add(x => x.UserEnrollment, Answer(e, false)).Add(x => x.IsPastEvent, true));

        cut.Find(".item-status").TextContent.Should().Contain("Não fui");
        HasButton(cut, "Remover inscrição").Should().BeFalse();
    }

    [Fact]
    public void EventCard_CancelledStatusOverridesAnswer_AndHidesToggle()
    {
        var e = Upcoming();
        e.Cancel("Weather");
        var cut = RenderCard(e, p => p.Add(x => x.UserEnrollment, Answer(e, true)));

        cut.Find(".item-status").TextContent.Should().Contain("Cancelado");
        HasButton(cut, "Vou").Should().BeFalse();
        cut.FindAll(".item-card__links button").Select(b => b.TextContent.Trim()).Should().Equal("Detalhes");
    }

    #endregion

    #region Secondary actions

    [Fact]
    public void EventCard_ShowsCountsAsNamedButtons()
    {
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.EnrollmentCount, 15)
            .Add(x => x.RepertoireCount, 4)
            .Add(x => x.DiscussionCount, 2));

        HasButton(cut, "Detalhes").Should().BeTrue();
        HasButton(cut, "Participantes (15)").Should().BeTrue();
        HasButton(cut, "Repertório (4)").Should().BeTrue();
        HasButton(cut, "Discussão (2)").Should().BeTrue();
    }

    [Fact]
    public void EventCard_InvokesSecondaryCallbacks()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.OnViewDetails, EventCallback.Factory.Create(this, () => calls.Add("details")))
            .Add(x => x.OnViewEnrollments, EventCallback.Factory.Create(this, () => calls.Add("enrollments")))
            .Add(x => x.OnWatchRepertoire, EventCallback.Factory.Create(this, () => calls.Add("repertoire")))
            .Add(x => x.OnViewDiscussion, EventCallback.Factory.Create(this, () => calls.Add("discussion"))));

        Button(cut, "Detalhes").Click();
        Button(cut, "Participantes (0)").Click();
        Button(cut, "Repertório (0)").Click();
        Button(cut, "Discussão (0)").Click();

        calls.Should().Equal("details", "enrollments", "repertoire", "discussion");
    }

    [Fact]
    public void EventCard_PastFestival_ShowsTrophiesAndVideos()
    {
        var cut = RenderCard(Past(type: EventType.Festival), p => p
            .Add(x => x.IsPastEvent, true)
            .Add(x => x.ShowTrophy, true)
            .Add(x => x.TrophyCount, 2)
            .Add(x => x.VideoCount, 3));

        HasButton(cut, "Prémios (2)").Should().BeTrue();
        HasButton(cut, "Vídeos (3)").Should().BeTrue();
    }

    [Fact]
    public void EventCard_PublicTrophyMode_ShowsOnlyTrophiesAndVideos()
    {
        var cut = RenderCard(Past(type: EventType.Festival), p => p
            .Add(x => x.IsPastEvent, true)
            .Add(x => x.ShowButtons, false)
            .Add(x => x.ShowOnlyTrophy, true));

        cut.FindAll("button").Select(b => b.GetAttribute("aria-label")).Should().Equal("Prémios (0)", "Vídeos (0)");
    }

    [Fact]
    public void EventCard_NoButtons_WhenShowButtonsIsFalse()
    {
        RenderCard(Upcoming(), p => p.Add(x => x.ShowButtons, false)).FindAll("button").Should().BeEmpty();
    }

    #endregion

    #region Management menu

    [Fact]
    public void EventCard_NoManagementMenu_ForNonAdmin()
    {
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.IsAdmin, false));

        cut.FindAll(".dropdown-menu").Should().BeEmpty();
        cut.Markup.Should().NotContain("Notificar por push");
    }

    [Fact]
    public void EventCard_AdminMenu_UpcomingEvent_HasAllActions_DestructiveLast()
    {
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.IsAdmin, true));

        cut.Find("[data-bs-toggle=dropdown]").GetAttribute("aria-label").Should().Be("Gerir: Test Event");
        MenuItems(cut).Should().Equal("Editar evento", "Notificar por push", "Notificar por email", "Cancelar evento", "Eliminar evento");
    }

    [Fact]
    public void EventCard_AdminMenu_RespectsRestrictedPermissions()
    {
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.IsAdmin, true)
            .Add(x => x.ShowDeleteButton, false)
            .Add(x => x.ShowCancelButton, false));

        MenuItems(cut).Should().Equal("Editar evento", "Notificar por push", "Notificar por email");
    }

    [Fact]
    public void EventCard_AdminMenu_PastEvent_OnlyEditAndDelete()
    {
        var cut = RenderCard(Past(), p => p.Add(x => x.IsAdmin, true).Add(x => x.IsPastEvent, true));

        MenuItems(cut).Should().Equal("Editar evento", "Eliminar evento");
    }

    [Fact]
    public void EventCard_AdminMenu_CancelledEvent_OffersReactivate_NoNotifications()
    {
        var e = Upcoming();
        e.Cancel("Weather");
        var cut = RenderCard(e, p => p.Add(x => x.IsAdmin, true));

        MenuItems(cut).Should().Equal("Editar evento", "Reativar evento", "Eliminar evento");
    }

    [Fact]
    public void EventCard_AdminMenu_InvokesCallbacks()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.IsAdmin, true)
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, () => calls.Add("edit")))
            .Add(x => x.OnSendPushNotification, EventCallback.Factory.Create(this, () => calls.Add("push")))
            .Add(x => x.OnSendEmail, EventCallback.Factory.Create(this, () => calls.Add("email")))
            .Add(x => x.OnCancelEvent, EventCallback.Factory.Create(this, () => calls.Add("cancel")))
            .Add(x => x.OnDelete, EventCallback.Factory.Create(this, () => calls.Add("delete"))));

        foreach (var item in new[] { "Editar evento", "Notificar por push", "Notificar por email", "Cancelar evento", "Eliminar evento" })
        {
            Button(cut, item).Click();
        }

        calls.Should().Equal("edit", "push", "email", "cancel", "delete");
    }

    #endregion
}
