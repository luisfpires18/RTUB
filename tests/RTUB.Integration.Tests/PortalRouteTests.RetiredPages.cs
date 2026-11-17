// PortalRouteTests: RetiredPages tests.
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests;

public partial class PortalRouteTests
{
    /// <summary>
    /// 013: the discussion and contact tracking are React; no Blazor component routes anything under /events.
    /// A visitor is sent to sign in (and back), as the Blazor pages did; GET/HEAD only.
    /// </summary>
    [Theory]
    [InlineData("/events/12/discussion")]
    [InlineData("/events/12/contacts")]
    public async Task EventDiscussionAndContacts_AreReact_AndSendVisitorsToSignIn(string path)
    {
        var client = NoRedirectClient();
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(path));
        (await client.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        var blazorEventRoutes = typeof(RTUB.Program).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
                .Cast<Microsoft.AspNetCore.Components.RouteAttribute>())
            .Select(r => r.Template)
            .Where(t => t.StartsWith("/events", StringComparison.OrdinalIgnoreCase))
            .ToList();
        blazorEventRoutes.Should().BeEmpty("no Blazor event page is left (013)");
    }

    /// <summary>
    /// 014: Rehearsals are React. Rehearsals speak of presença (attendance), events of inscrição (enrollment);
    /// neither borrows the other's word. The Blazor page and its rehearsal-only parts are gone, and the Blazor
    /// menu opens the React page with a full load.
    /// </summary>
    [Fact]
    public void Rehearsals_AreReact_WithTheirOwnTerminology_AndTheBlazorPageIsGone()
    {
        var src = Path.Combine(FindRepoRoot(), "src");
        var portal = Path.Combine(src, "RTUB.Web", "portal", "src");
        var rehearsals = Directory.GetFiles(portal, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f).StartsWith("Rehearsal", StringComparison.Ordinal)
                || f.EndsWith("rehearsalsApi.ts")).Select(File.ReadAllText).ToList();
        rehearsals.Should().HaveCount(9, "the 4 rehearsal files (page, detail, dialogs, api) became 9 in the 113 split");
        rehearsals.Should().NotContain(t => t.Contains("inscri", StringComparison.OrdinalIgnoreCase) || t.Contains("enrollment", StringComparison.OrdinalIgnoreCase)
            || t.Contains("migra", StringComparison.OrdinalIgnoreCase), "rehearsals speak of presença, never inscrição");
        string.Concat(rehearsals).Should().Contain("Presenças").And.Contain("As minhas presenças").And.Contain("presença");

        var events = Directory.GetFiles(portal, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f).StartsWith("Event", StringComparison.Ordinal) || f.EndsWith("eventsApi.ts"))
            .Select(File.ReadAllText).ToList();
        events.Should().NotContain(t => t.Contains("presença", StringComparison.OrdinalIgnoreCase) || t.Contains("attendance", StringComparison.OrdinalIgnoreCase),
            "events speak of inscrição, never presença");

        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().Contain("'/rehearsals': lazy(").And.Contain("RehearsalDetail");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Rehearsals.razor", "RTUB.Shared/Components/Cards/RehearsalCard.razor",
                     "RTUB.Shared/Components/Modals/ParticipationModal.razor", "RTUB.Shared/Components/UI/InstrumentCounter.razor",
                     "RTUB.Application/Services/RehearsalFilterService.cs", "RTUB.Application/Services/RehearsalUrlService.cs",
                     "RTUB.Application/Services/RehearsalAttendanceFilterService.cs", "RTUB.Application/Services/RehearsalStatisticsService.cs",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 014", gone);
        }
    }

    [Fact]
    public void EventPage_LinksTheReactDiscussionAndContacts_AndTheOldBlazorPartsAreGone()
    {
        var src = Path.Combine(FindRepoRoot(), "src");
        var portal = Path.Combine(src, "RTUB.Web", "portal", "src");
        var detail = PortalSource.Cat(portal, "pages/EventDetail.tsx", "components/EventDetail*.tsx");
        detail.Should().Contain("portal.eventDiscussion(event.id)").And.Contain("portal.eventContacts(event.id)")
            .And.Contain("{detail.canTrackContacts && (").And.NotContain("legacy.");
        File.ReadAllText(Path.Combine(portal, "data/content.ts")).Should().NotContain("legacy.eventDiscussion").And.Contain("eventContacts: (id: number)");
        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().Contain("(discussion|contacts)");

        var talk = PortalSource.Cat(portal, "pages/EventDiscussion.tsx", "components/EventDiscussion*.tsx");
        talk.Should().Contain("Escreve uma nota para a atuação…").And.Contain("Ainda não há mensagens nesta atuação.")
            .And.Contain("Deixa uma nota, combina detalhes ou partilha uma dúvida.").And.Contain("Apagar esta publicação?");
        var contacts = File.ReadAllText(Path.Combine(portal, "pages/EventContacts.tsx"));
        foreach (var page in new[] { talk, contacts })
        {
            page.Should().NotContainAny(new[] { "attendance", "presença", "Presença", "migra" });
        }

        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/EventDiscussion.razor", "RTUB.Web/Pages/Activities/EventContacts.razor",
                     "RTUB.Shared/Components/Discussion/PostCard.razor", "RTUB.Shared/Components/Discussion/PostComposer.razor",
                     "RTUB.Shared/Components/Discussion/CommentItem.razor", "RTUB.Application/Services/EventDiscussionService.cs",
                     "RTUB.Application/Services/EventAuthorizationService.cs",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 013", gone);
        }
    }

    [Fact]
    public void EventsLinks_GoToTheReactAgenda()
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src", "RTUB.Web", "portal", "src");

        File.ReadAllText(Path.Combine(src, "data/content.ts")).Should().Contain("events: '/events'").And.NotContain("memberEvents");
        PortalSource.Home(src).Should().Contain("<MoreLink href={portal.events}>",
            "the home agenda's call to action opens the React agenda");
        File.ReadAllText(Path.Combine(src, "data/sections.ts")).Should().Contain("events: portal.events,",
            "the top bar, the mobile menu and the footer open the agenda page, not the home section");
        File.ReadAllText(Path.Combine(src, "components/RequestDialogs.tsx")).Should().Contain("href={approved.createEventUrl}",
            "turning a request into an event opens the React create form (012F; the requests page is React since 031, "
            + "RequestsApiTests pins the /events?openModal=true link)");
    }

    /// <summary>032: the Blazor profile and map are retired; their old URLs land on the React pages, for everyone.</summary>
    [Theory]
    [InlineData("/member/profile", "/profile")]
    [InlineData("/member/map", "/members/map")]
    public async Task RetiredMemberProfileAndMap_RedirectToTheReactPages(string path, string target)
    {
        var response = await NoRedirectClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "temporary (302) while DEV is hybrid");
        response.Headers.Location!.ToString().Should().Be(target);
    }

    // ---------- retired Blazor Music (React track 006) ----------

    [Theory]
    [InlineData("/music/songs/12", "/music/albums/12")]
    [InlineData("/music/songs/3?from=share", "/music/albums/3?from=share")]
    public async Task RetiredBlazorAlbumPage_RedirectsToTheReactAlbum(string path, string target)
    {
        var response = await NoRedirectClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be(target);
    }

    [Fact]
    public void HomeMusicLink_GoesToTheReactMusicPage()
    {
        var src = Path.Combine(FindRepoRoot(), "src", "RTUB.Web", "portal", "src");

        File.ReadAllText(Path.Combine(src, "data/content.ts")).Should().Contain("music: '/music'");
        PortalSource.Home(src).Should().Contain("<MoreLink href={portal.music}>");
        File.ReadAllText(Path.Combine(src, "data/sections.ts")).Should().Contain("music: portal.music,",
            "the top bar, the mobile menu and the footer open the Music page, not the home section");
        Directory.GetFiles(src, "*", SearchOption.AllDirectories).Select(File.ReadAllText).Should().NotContain(t => Regex.IsMatch(t, "(?<!/api)/music/songs/"),
            "the React Music area links to /music/albums/{id}, never the retired Blazor route");
    }

    // ---------- retired Blazor /request (React track 003) ----------

    [Fact]
    public async Task RetiredBlazorRequest_HasNoSubmissionPathLeft()
    {
        var response = await NoRedirectClient().PostAsync("/request",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            "only the React shell (GET/HEAD) exists at /request; submissions go to POST /api/public/requests");
    }
}
