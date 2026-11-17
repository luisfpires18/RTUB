// PendingActionsService: panels.
using System.Security.Claims;
using FluentAssertions;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class PendingActionsServiceTests
{
    // ---------- access ----------

    [Fact]
    public async Task ActiveLeitao_GetsBothParticipationPanels_AndTheHeader()
    {
        var me = AddUser([MemberCategory.Leitao], retired: false);

        var page = (await Service().GetPageAsync(SignedIn(me))).Value!;

        page.Status.Should().Be("active");
        page.Today.Should().Be("2026-11-06");
        page.FiscalYear.Should().Be("2026-2027");
        page.Panels.Select(p => p.Key).Should().Equal("events", "rehearsals");
        page.Panels.Should().OnlyContain(p => p.Category == "participation" && p.PageSize == PendingActionsService.PageSize);
    }

    [Fact]
    public async Task Reformado_AndAccountsWithoutCategory_GetThePage_WithoutParticipationPanels()
    {
        AddEvent("Serenata", Today.AddDays(3));
        var retired = AddUser([MemberCategory.Tuno], retired: true);
        var none = AddUser([], retired: false);

        var r = await Service().GetPageAsync(SignedIn(retired));
        var n = (await Service().GetPageAsync(SignedIn(none))).Value!;

        r.Status.Should().Be(EventResultStatus.NotFound, "087: the page is not for a Reformado at all");
        (await Service().CountPendingAsync(retired)).Should().Be(0, "no reminder push either");
        n.Status.Should().Be("none");
        n.Panels.Should().BeEmpty();
        (await Service().GetPanelAsync(SignedIn(retired), "events", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public async Task Visitors_ExpelledAndUnknownAccounts_AreRefused()
    {
        var expelled = AddUser([MemberCategory.Tuno], retired: false, expelled: true);

        (await Service().GetPageAsync(new ClaimsPrincipal(new ClaimsIdentity()))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().GetPageAsync(SignedIn(expelled))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().GetPageAsync(SignedIn("ghost"))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().CountPendingAsync("ghost")).Should().Be(0);
    }

    // ---------- what is pending ----------

    [Fact]
    public async Task Events_UpcomingIsEveryUnansweredOne_PastIsThisFiscalYearOnly()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var soon = AddEvent("Amanhã", Today.AddDays(1));
        var far = AddEvent("Daqui a meio ano", Today.AddDays(200));
        var going = AddEvent("Já disse que vou", Today.AddDays(2));
        var notGoing = AddEvent("Já disse que não", Today.AddDays(4));
        AddEvent("Cancelada", Today.AddDays(5), cancelled: true);
        var today = AddEvent("Hoje", Today.AddHours(21));
        var september = AddEvent("Setembro", new DateTime(2026, 9, 1, 21, 0, 0));
        AddEvent("Ano passado", new DateTime(2026, 8, 31, 21, 0, 0));
        AddEnrollment(going, me, true);
        AddEnrollment(notGoing, me, false);

        var upcoming = (await Service().GetPanelAsync(SignedIn(me), "events", "upcoming", 1)).Value!;
        var past = (await Service().GetPanelAsync(SignedIn(me), "events", "past", 1)).Value!;

        upcoming.Total.Should().Be(3);
        upcoming.Rows.Select(r => r.SourceKey).Should().Equal(Key(today), Key(soon), Key(far));
        upcoming.Rows[0].Href.Should().Be($"/events/{today}");
        upcoming.Rows[0].Actions.Select(a => (a.Label, a.Answer)).Should().Equal(("Vou", "yes"), ("Não vou", "no"));
        past.Rows.Single().Actions.Select(a => (a.Label, a.Answer)).Should().Equal(("Fui", "yes"), ("Não fui", "no"));
        upcoming.Rows.Should().OnlyContain(r => !r.Required && !r.Dismissible);
        past.Total.Should().Be(1, "the fiscal year starts on 1 September");
        past.Rows.Single().SourceKey.Should().Be(Key(september));
        past.Filters.Select(f => (f.Key, f.Count)).Should().Equal(("upcoming", 3), ("past", 1));
    }

    [Fact]
    public async Task Rehearsals_SameRules_AndAnotherMembersAnswerDoesNotResolveMine()
    {
        var me = AddUser([MemberCategory.Caloiro], retired: false);
        var other = AddUser([MemberCategory.Caloiro], retired: false);
        var next = AddRehearsal(Today.AddDays(3));
        var answered = AddRehearsal(Today.AddDays(5));
        AddRehearsal(Today.AddDays(6), cancelled: true);
        var lastWeek = AddRehearsal(Today.AddDays(-7));
        AddRehearsal(new DateTime(2026, 6, 1));
        AddAttendance(answered, me);
        AddAttendance(next, other);

        var upcoming = (await Service().GetPanelAsync(SignedIn(me), "rehearsals", "upcoming", 1)).Value!;
        var past = (await Service().GetPanelAsync(SignedIn(me), "rehearsals", "past", 1)).Value!;

        upcoming.Rows.Select(r => r.SourceKey).Should().Equal(Key(next));
        upcoming.Rows.Single().Href.Should().Be($"/rehearsals/{next}");
        past.Rows.Select(r => r.SourceKey).Should().Equal(Key(lastWeek));
        (await Service().CountPendingAsync(me)).Should().Be(2);
        (await Service().GetPanelAsync(SignedIn(other), "rehearsals", "upcoming", 1)).Value!.Rows.Select(r => r.SourceKey)
            .Should().Equal([Key(answered)], "each member's own answers only");
    }

    // ---------- paging ----------

    [Fact]
    public async Task Panels_PageThreeRowsAtATime_IndependentlyAndClamped()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var ids = Enumerable.Range(1, 7).Select(i => AddEvent($"E{i}", Today.AddDays(i))).ToList();
        AddRehearsal(Today.AddDays(1));
        var service = Service();

        var page1 = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 1)).Value!;
        var page3 = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 3)).Value!;
        var beyond = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 99)).Value!;
        var rehearsals = (await service.GetPanelAsync(SignedIn(me), "rehearsals", "upcoming", 1)).Value!;

        page1.Rows.Select(r => r.SourceKey).Should().Equal(ids.Take(3).Select(Key));
        page1.Total.Should().Be(7);
        page3.Rows.Select(r => r.SourceKey).Should().Equal(Key(ids[6]));
        beyond.Page.Should().Be(3, "a page past the end answers the last one");
        rehearsals.Total.Should().Be(1, "the other panel pages on its own");

        // The only row of the last page is answered elsewhere: asking for page 3 again answers page 2, with the new count.
        AddEnrollment(ids[6], me, true);
        var after = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 3)).Value!;
        after.Page.Should().Be(2);
        after.Total.Should().Be(6);
        after.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task UnknownFilter_IsTheDefault_AndUnknownPanel_Is404()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        AddEvent("Próxima", Today.AddDays(1));

        var panel = (await Service().GetPanelAsync(SignedIn(me), "events", "everything", 0)).Value!;

        panel.Filter.Should().Be("upcoming");
        panel.Page.Should().Be(1);
        (await Service().GetPanelAsync(SignedIn(me), "inbox", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
    }
}
