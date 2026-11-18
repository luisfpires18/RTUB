// PortalRouteTests: the React event pages, their answers and their management.
using System.Net;
using FluentAssertions;

namespace RTUB.Integration.Tests;

public partial class PortalRouteTests
{

    // ---------- Blazor member/admin bridges ----------

    /// <summary>
    /// Events use "enrollment" (Inscrições); "attendance" (Presenças) belongs to rehearsals. A member
    /// answers in a modal on the event page; /events/{id}/enrollment, /events/{id}/enrollments and
    /// /events/{id}/attendance are not pages (404).
    /// </summary>
    [Fact]
    public async Task EventAnswers_AreAModalOnTheEventPage_NotAPageOfTheirOwn()
    {
        var client = NoRedirectClient();

        (await client.GetAsync("/events/12/enrollment")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/events/12/enrollment", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Everyone's answers are the event page's "Quem vai / Quem foi" (and its Admin/Owner manager).
        (await client.GetAsync("/events/12/enrollments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/events/my-enrollments")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the page is gone: \"Onde estiveste\" is a modal on /events");
        (await client.GetAsync("/events/1/attendance")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/events/1/attendance")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var root = FindRepoRoot();
        var portal = Path.Combine(root, "src", "RTUB.Web", "portal", "src");
        Directory.GetFiles(portal, "EventEnrollment.tsx", SearchOption.AllDirectories).Should().BeEmpty("the standalone answer page was retired");
        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().NotContain("EventEnrollment").And.NotContain("/enrollment'");
        var events = Directory.GetFiles(portal, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f).StartsWith("Event", StringComparison.Ordinal)
                || f.EndsWith("eventsApi.ts"))
            .Select(File.ReadAllText).ToList();
        events.Should().NotContain(t => t.Contains("attendance", StringComparison.OrdinalIgnoreCase),
            "event pages speak of enrollment, never attendance");
        string.Concat(events).Should().Contain("<EnrollmentDialog").And.Contain("<PrizesDialog").And.Contain("className={`quick-reply");
    }

    /// <summary>
    /// 011.5: the event page says "Quem foi" once the event is over and never sends anyone to the
    /// Blazor management page; the agenda's cards carry no "Quem vai" action, and create/edit/delete
    /// are React modals shown only when the server says the caller can manage (and enforced there).
    /// </summary>
    [Fact]
    public void EventPages_UsePastWording_AndManageInReact()
    {
        var src = Path.Combine(FindRepoRoot(), "src", "RTUB.Web", "portal", "src");
        // 113: the page, its dialogs and the manage code are split into parts; each variable is every part of that feature.
        var detail = PortalSource.Cat(src, "pages/EventDetail.tsx", "components/EventDetail*.tsx");
        var agenda = PortalSource.Cat(src, "pages/Events.tsx", "components/EventsCards.tsx", "components/EventsToolbar.tsx");
        var manage = PortalSource.Cat(src, "components/EventManage*.tsx");

        detail.Should().Contain("past ? 'Quem foi' : 'Quem vai'").And.Contain("whoLabel(past)");
        detail.Should().NotContain("Gerir atuações").And.NotContain("legacy.memberEvents").And.NotContain("eventEnrollments",
            "the event page sends nobody to the Blazor management pages");

        var cards = File.ReadAllText(Path.Combine(src, "components/EventsCards.tsx"));
        var card = cards[cards.IndexOf("function AgendaCard", StringComparison.Ordinal)..cards.IndexOf("function ArchiveRow", StringComparison.Ordinal)];
        card.Should().NotContain("Quem vai", "who is going lives on the event page only");
        agenda.Should().Contain("{agenda?.canEdit && (").And.Contain("Adicionar atuação")
            .And.Contain("onEdit: () => setEditing(e.id),").And.Contain("onDelete: () => setDeleting(e),")
            .And.Contain("onNotice: e.past || e.cancelled ? undefined", "notices only for upcoming, not cancelled dates (012A)")
            .And.Contain("onCancel: e.past ? undefined", "cancel / reactivate only for upcoming dates (012A)");
        agenda.Should().NotContain("Gerir atuações");
        manage.Should().Contain("eventsApi.createEvent(").And.Contain("eventsApi.updateEvent(").And.Contain("eventsApi.deleteEvent(");
        manage.Should().Contain("eventsApi.setImage(").And.Contain("eventsApi.removeImage(").And.Contain("eventsApi.cancelEvent(")
            .And.Contain("eventsApi.reactivateEvent(").And.Contain("eventsApi.sendNotice(")
            .And.NotContain("área de membros", "image, cancel and notices no longer send anyone to /member/events (012A)");
        detail.Should().Contain("<NoticeDialog").And.Contain("<CancelEventDialog").And.Contain("<ReactivateEventDialog");
        detail.Should().Contain("loaded.canManagePrizes", "Admin/Owner manage prizes in the Prémios modal (012B)");
        File.ReadAllText(Path.Combine(src, "components/EventPrizesDialog.tsx")).Should().Contain("<PrizeManager");
        manage.Should().Contain("eventsApi.addPrize(").And.Contain("eventsApi.renamePrize(").And.Contain("eventsApi.deletePrize(");
        manage.Should().Contain("eventsApi.uploadVideo(").And.Contain("eventsApi.renameVideo(").And.Contain("eventsApi.reorderVideos(")
            .And.Contain("eventsApi.deleteVideo(");
        detail.Should().Contain("<VideoManagerDialog").And.Contain("Gerir vídeos");
        detail.Should().Contain("<RepertoireManagerDialog").And.Contain("Gerir repertório");
        detail.Should().Contain("<ParticipantsManagerDialog").And.Contain("Gerir inscrições")
            .And.NotContain("legacy.eventEnrollments");
        manage.Should().Contain("eventsApi.addEnrollment(").And.Contain("eventsApi.removeMemberEnrollment(").And.Contain("eventsApi.enrollmentMembers(");
        File.ReadAllText(Path.Combine(src, "main.tsx")).Should().NotContain("my-enrollments");
        Directory.GetFiles(src, "MyEnrollments.tsx", SearchOption.AllDirectories).Should().BeEmpty("\"Onde estiveste\" is a modal on /events now");
        File.ReadAllText(Path.Combine(src, "components/EventStats.tsx")).Should().Contain("export function WhereYouWereDialog");
        manage.Should().Contain("eventsApi.addToRepertoire(").And.Contain("eventsApi.removeFromRepertoire(")
            .And.Contain("eventsApi.reorderRepertoire(").And.Contain("eventsApi.clearRepertoireDay(").And.Contain("eventsApi.repertoireSongs(");
    }

    /// <summary>
    /// 012F: /member/events and what only it used are gone; its statistics are the agenda's members-only
    /// "Estatísticas" modal (GET /api/events/stats); nothing links to it any more. Discussion (members) and
    /// contacts (Mod and above) stay Blazor bridges, linked from the React event page.
    /// </summary>
    [Fact]
    public void MemberEvents_IsGone_WithItsBridgeOnlyParts()
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Members/MemberEvents.razor", "RTUB.Web/Pages/Members/MemberEvents.razor.css",
                     "RTUB.Shared/Components/Modals/RepertoireModal.razor", "RTUB.Shared/Components/UI/EnrollmentStatisticsButton.razor",
                     "RTUB.Application/Services/EventStatisticsService.cs", "RTUB.Application/Services/EnrollmentStatisticsService.cs",
                     "RTUB.Application/Services/EventFilterService.cs", "RTUB.Application/Services/EventUrlService.cs",
                     "RTUB.Web/Pages/Activities/EventEnrollments.razor", "RTUB.Shared/Components/UI/MyEnrollmentsButton.razor",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired", gone);
        }

        var sep = Path.DirectorySeparatorChar;
        var linking = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor") || f.EndsWith(".tsx") || f.EndsWith(".ts"))
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}") && !f.Contains("node_modules")
                && !f.Contains($"wwwroot{sep}portal"))
            .Where(f => File.ReadAllText(f) is var text && (text.Contains("\"/member/events") || text.Contains("'/member/events")))
            .Select(Path.GetFileName)
            .ToList();
        linking.Should().BeEmpty("no Blazor or React page links to the retired /member/events");

        var portal = Path.Combine(src, "RTUB.Web", "portal", "src");
        var agenda = PortalSource.Cat(portal, "pages/Events.tsx", "components/EventsCards.tsx", "components/EventsToolbar.tsx");
        agenda.Should().Contain("<StatsDialog").And.Contain("onClick={() => setStats(true)}").And.NotContain("Área de membros");
        // EventDialogs.tsx was split (113) into EventAnswer, EventPrizesDialog and EventStats.
        PortalSource.Cat(portal, "components/EventAnswer.tsx", "components/EventPrizesDialog.tsx", "components/EventStats.tsx")
            .Should().Contain("eventsApi.stats(").And.NotContain("presen");
        agenda.Should().Contain("takeCreatePrefill").And.Contain("prefill={editing === 'new' ? prefill : undefined}",
            "Requests' prefill opens the React create form (Admin/Owner)");
        // 030: the member menu (MemberShell.tsx) is the members' way around; /profile keeps the enrollments and sign-out.
        var shell = File.ReadAllText(Path.Combine(portal, "components/MemberShell.tsx"));
        shell.Should().Contain("href: portal.events,").And.NotContain("portal.myEnrollments").And.Contain("signOut()")
            .And.NotContain("memberEvents");
        File.ReadAllText(Path.Combine(portal, "pages/Profile.tsx")).Should().NotContain("portal.myEnrollments")
            .And.Contain("<SignOutButton").And.NotContain("memberEvents");

    }
}
