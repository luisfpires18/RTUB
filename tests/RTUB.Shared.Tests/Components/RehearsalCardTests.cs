using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// RehearsalCard behavior: date-first content, the member's attendance answer (none / pending /
/// approved / not going), secondary actions and the management menu
/// (docs/design/RTUB_UI_REFACTOR.md section 25). Queries by accessible names and ARIA state.
/// </summary>
public class RehearsalCardTests : BunitContext
{
    private static Rehearsal Upcoming(string? theme = null) => Rehearsal.Create(DateTime.Today.AddDays(3), "Music Room", theme);

    private static Rehearsal Past() => Rehearsal.Create(DateTime.Today.AddDays(-3), "Music Room");

    private static RehearsalAttendance Attendance(bool willAttend, bool attended = false)
    {
        var a = RehearsalAttendance.Create(1, "user123");
        a.WillAttend = willAttend;
        a.Attended = attended;
        return a;
    }

    private IRenderedComponent<RehearsalCard> RenderCard(Rehearsal r, Action<ComponentParameterCollectionBuilder<RehearsalCard>>? more = null) =>
        Render<RehearsalCard>(parameters =>
        {
            parameters.Add(p => p.Rehearsal, r);
            more?.Invoke(parameters);
        });

    // Visible text (first span) or aria-label; "Vou pendente" matches "Vou"
    private static IElement Button(IRenderedComponent<RehearsalCard> cut, string name) =>
        cut.FindAll("button").Single(b => ButtonName(b) == name);

    private static string ButtonName(IElement b) =>
        b.GetAttribute("aria-label") ?? b.QuerySelector("span")?.TextContent.Trim() ?? b.TextContent.Trim();

    private static bool HasButton(IRenderedComponent<RehearsalCard> cut, string name) =>
        cut.FindAll("button").Any(b => ButtonName(b) == name);

    private static List<string> MenuItems(IRenderedComponent<RehearsalCard> cut) =>
        cut.FindAll(".dropdown-menu button").Select(b => b.TextContent.Trim()).ToList();

    #region Content

    [Fact]
    public void RehearsalCard_ShowsDateTimeWeekdayAndLocation()
    {
        var r = Rehearsal.Create(new DateTime(2030, 3, 5), "Music Room"); // a Tuesday
        var cut = RenderCard(r);

        cut.Find("time").GetAttribute("datetime").Should().Be("2030-03-05");
        cut.Find("h3").TextContent.Should().Contain("Terça-feira").And.Contain("05 Mar 2030");
        cut.Find("article").GetAttribute("aria-labelledby").Should().Be(cut.Find("h3").Id);
        cut.Markup.Should().Contain("21:30 - 00:00").And.Contain("Music Room");
    }

    [Fact]
    public void RehearsalCard_RendersThemeAndDescription_WhenProvided()
    {
        var r = Upcoming("Fado practice");
        r.Description = "Bring the new scores";
        var cut = RenderCard(r);

        var terms = cut.FindAll("dt").Select(d => d.TextContent).ToList();
        terms.Should().Equal("Tema", "Descrição");
        cut.FindAll("dd").Select(d => d.TextContent).Should().Equal("Fado practice", "Bring the new scores");
    }

    [Fact]
    public void RehearsalCard_NoNotesList_WhenNoThemeOrDescription()
    {
        RenderCard(Upcoming()).FindAll("dl").Should().BeEmpty();
    }

    [Fact]
    public void RehearsalCard_MarksNextRehearsal()
    {
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.IsNext, true));

        cut.Find(".item-status").TextContent.Should().Contain("Próximo ensaio");
        cut.Find("article").ClassList.Should().Contain("rehearsal-item--next");
    }

    [Fact]
    public void RehearsalCard_Cancelled_ShowsStatusAndOnlyDetails_ForMember()
    {
        var r = Upcoming();
        r.Cancel("Feriado");
        var cut = RenderCard(r, p => p.Add(x => x.UserAttendance, Attendance(true)).Add(x => x.IsNext, true));

        cut.Find(".item-status").TextContent.Should().Contain("Cancelado");
        cut.FindAll("[role=group]").Should().BeEmpty();
        cut.FindAll("button").Select(ButtonName).Should().Equal("Detalhes");
        cut.Find("article").ClassList.Should().NotContain("rehearsal-item--next");
    }

    [Fact]
    public void RehearsalCard_Cancelled_AdminStillSeesAttendances()
    {
        var r = Upcoming();
        r.Cancel("Feriado");
        var cut = RenderCard(r, p => p.Add(x => x.IsAdmin, true).Add(x => x.AttendanceCount, 4));

        HasButton(cut, "Ver presenças (4)").Should().BeTrue();
    }

    #endregion

    #region Member attendance answer

    [Fact]
    public void RehearsalCard_NoAnswer_BothOptionsUnpressed_AndCallTheCreateCallbacks()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.OnAttendPending, EventCallback.Factory.Create(this, () => calls.Add("pending")))
            .Add(x => x.OnAttendNotGoing, EventCallback.Factory.Create(this, () => calls.Add("notgoing")))
            .Add(x => x.OnEditAttendance, EventCallback.Factory.Create(this, () => calls.Add("edit"))));

        Button(cut, "Vou").GetAttribute("aria-pressed").Should().Be("false");
        Button(cut, "Vou").GetAttribute("title").Should().Be("Marcar presença");
        Button(cut, "Não vou").GetAttribute("aria-pressed").Should().Be("false");

        Button(cut, "Vou").Click();
        Button(cut, "Não vou").Click();

        calls.Should().Equal("pending", "notgoing");
    }

    [Fact]
    public void RehearsalCard_PendingAttendance_VouPressedWithPendingNote_ClickEdits()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.UserAttendance, Attendance(true, attended: false))
            .Add(x => x.OnEditAttendance, EventCallback.Factory.Create(this, () => calls.Add("edit")))
            .Add(x => x.OnAttendNotGoing, EventCallback.Factory.Create(this, () => calls.Add("notgoing"))));

        var vou = Button(cut, "Vou");
        vou.GetAttribute("aria-pressed").Should().Be("true");
        vou.TextContent.Should().Contain("pendente");
        vou.GetAttribute("title").Should().Be("Editar presença pendente");
        Button(cut, "Não vou").GetAttribute("title").Should().Be("Mudar para não vou");

        vou.Click();
        Button(cut, "Não vou").Click();

        calls.Should().Equal("edit", "notgoing");
    }

    [Fact]
    public void RehearsalCard_ApprovedAttendance_VouPressedWithoutPendingNote()
    {
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.UserAttendance, Attendance(true, attended: true)));

        var vou = Button(cut, "Vou");
        vou.GetAttribute("aria-pressed").Should().Be("true");
        vou.TextContent.Should().NotContain("pendente");
        vou.GetAttribute("title").Should().Be("Editar presença aprovada");
    }

    [Fact]
    public void RehearsalCard_NotGoing_NaoVouPressed_ClickEdits_VouSwitches()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.UserAttendance, Attendance(false))
            .Add(x => x.OnEditAttendance, EventCallback.Factory.Create(this, () => calls.Add("edit")))
            .Add(x => x.OnAttendPending, EventCallback.Factory.Create(this, () => calls.Add("pending"))));

        Button(cut, "Não vou").GetAttribute("aria-pressed").Should().Be("true");
        Button(cut, "Não vou").GetAttribute("title").Should().Be("Editar impossibilidade");
        Button(cut, "Vou").GetAttribute("title").Should().Be("Mudar para vou");

        Button(cut, "Não vou").Click();
        Button(cut, "Vou").Click();

        calls.Should().Equal("edit", "pending");
    }

    [Fact]
    public void RehearsalCard_Past_NoAnswerToggle()
    {
        RenderCard(Past(), p => p.Add(x => x.IsPastRehearsal, true)).FindAll("[role=group]").Should().BeEmpty();
    }

    [Fact]
    public void RehearsalCard_PastPending_ShowsStatusAndRemove()
    {
        var removed = false;
        var cut = RenderCard(Past(), p => p
            .Add(x => x.IsPastRehearsal, true)
            .Add(x => x.UserAttendance, Attendance(true, attended: false))
            .Add(x => x.OnRemoveAttendance, EventCallback.Factory.Create(this, () => removed = true)));

        cut.Find(".item-status").TextContent.Should().Contain("Presença pendente");
        var remove = Button(cut, "Remover");
        remove.GetAttribute("title").Should().Be("Remover presença pendente");
        remove.Click();
        removed.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true, "Fui")]
    [InlineData(false, false, "Não fui")]
    public void RehearsalCard_PastAnswered_ShowsStatus_NoRemove(bool willAttend, bool attended, string status)
    {
        var cut = RenderCard(Past(), p => p
            .Add(x => x.IsPastRehearsal, true)
            .Add(x => x.UserAttendance, Attendance(willAttend, attended)));

        cut.Find(".item-status").TextContent.Should().Contain(status);
        HasButton(cut, "Remover").Should().BeFalse();
    }

    #endregion

    #region Secondary actions

    [Fact]
    public void RehearsalCard_DetailsAndAttendances_InvokeCallbacks()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.AttendanceCount, 7)
            .Add(x => x.OnViewDetails, EventCallback.Factory.Create(this, () => calls.Add("details")))
            .Add(x => x.OnViewAttendances, EventCallback.Factory.Create(this, () => calls.Add("attendances"))));

        Button(cut, "Detalhes").Click();
        Button(cut, "Ver presenças (7)").Click();

        calls.Should().Equal("details", "attendances");
    }

    [Fact]
    public void RehearsalCard_AdminPastWithPendingApprovals_ShowsPendingReminder()
    {
        var cut = RenderCard(Past(), p => p
            .Add(x => x.IsAdmin, true)
            .Add(x => x.IsPastRehearsal, true)
            .Add(x => x.HasPendingApprovals, true)
            .Add(x => x.AttendanceCount, 3));

        var reminder = Button(cut, "Ver presenças pendentes para aprovar (3)");
        reminder.GetAttribute("title").Should().Contain("pendentes para aprovar");
        reminder.InnerHtml.Should().Contain("bi-clock-fill");
    }

    #endregion

    #region Management menu

    [Fact]
    public void RehearsalCard_NoMenu_ForMember()
    {
        RenderCard(Upcoming()).FindAll(".dropdown-menu").Should().BeEmpty();
    }

    [Fact]
    public void RehearsalCard_AdminMenu_Upcoming_DestructiveLast()
    {
        var cut = RenderCard(Upcoming(), p => p.Add(x => x.IsAdmin, true));

        cut.Find("[data-bs-toggle=dropdown]").GetAttribute("aria-label").Should().StartWith("Gerir: ensaio de ");
        MenuItems(cut).Should().Equal("Editar ensaio", "Notificar por push", "Cancelar ensaio", "Eliminar ensaio");
    }

    [Fact]
    public void RehearsalCard_AdminMenu_Past_CanStillCancel_NoPush()
    {
        var cut = RenderCard(Past(), p => p.Add(x => x.IsAdmin, true).Add(x => x.IsPastRehearsal, true));

        MenuItems(cut).Should().Equal("Editar ensaio", "Cancelar ensaio", "Eliminar ensaio");
    }

    [Fact]
    public void RehearsalCard_AdminMenu_CancelledUpcoming_OffersReactivate()
    {
        var r = Upcoming();
        r.Cancel("Feriado");

        MenuItems(RenderCard(r, p => p.Add(x => x.IsAdmin, true)))
            .Should().Equal("Editar ensaio", "Reativar ensaio", "Eliminar ensaio");
    }

    [Fact]
    public void RehearsalCard_AdminMenu_CancelledPast_NoReactivate()
    {
        var r = Past();
        r.Cancel("Feriado");

        MenuItems(RenderCard(r, p => p.Add(x => x.IsAdmin, true).Add(x => x.IsPastRehearsal, true)))
            .Should().Equal("Editar ensaio", "Eliminar ensaio");
    }

    [Fact]
    public void RehearsalCard_AdminMenu_RespectsRestrictedPermissions()
    {
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.IsAdmin, true)
            .Add(x => x.ShowDeleteButton, false)
            .Add(x => x.ShowCancelButton, false));

        MenuItems(cut).Should().Equal("Editar ensaio", "Notificar por push");
    }

    [Fact]
    public void RehearsalCard_AdminMenu_InvokesCallbacks()
    {
        var calls = new List<string>();
        var cut = RenderCard(Upcoming(), p => p
            .Add(x => x.IsAdmin, true)
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, () => calls.Add("edit")))
            .Add(x => x.OnSendPushNotification, EventCallback.Factory.Create(this, () => calls.Add("push")))
            .Add(x => x.OnCancel, EventCallback.Factory.Create(this, () => calls.Add("cancel")))
            .Add(x => x.OnDelete, EventCallback.Factory.Create(this, () => calls.Add("delete"))));

        foreach (var item in new[] { "Editar ensaio", "Notificar por push", "Cancelar ensaio", "Eliminar ensaio" })
        {
            cut.FindAll(".dropdown-menu button").Single(b => b.TextContent.Trim() == item).Click();
        }

        calls.Should().Equal("edit", "push", "cancel", "delete");
    }

    #endregion
}
