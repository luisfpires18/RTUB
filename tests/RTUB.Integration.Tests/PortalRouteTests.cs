using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Route ownership of the React public shell (React track 001-004, docs/react-portal-pilot.md).
/// React owns exactly /, /privacy, /profile, /request, /music, /login, /roles, /gallery and /events (011), served from the committed build in
/// wwwroot/portal; the pilot's /portal... URLs redirect there; every other page stays Blazor.
/// </summary>
public class PortalRouteTests : IntegrationTestBase
{
    public PortalRouteTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private HttpClient NoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/")]
    [InlineData("/privacy")]
    [InlineData("/profile")]
    [InlineData("/request")]
    [InlineData("/music")]
    [InlineData("/music/albums/1")]
    [InlineData("/login")]
    [InlineData("/login?ReturnUrl=%2Fprofile")]
    [InlineData("/roles")]
    [InlineData("/roles?fy=2024-2025")]
    [InlineData("/gallery")]
    [InlineData("/gallery?item=1")]
    [InlineData("/events")]
    [InlineData("/events?season=2025-2026&type=Festival")]
    [InlineData("/events/1")]
    public async Task ReactRoutes_ServeTheUncachedPortalShellUnderTheEnforcedCsp(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        response.Headers.CacheControl!.NoCache.Should().BeTrue("a cached shell would point at a previous deploy's hashed assets");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.Contain("id=\"splash\"");
        html.Should().NotContain("blazor.web.js", "{0} is React, not a Blazor page", path);
        html.Should().Contain("href=\"/manifest.webmanifest\"", "the portal must keep the one installed-app identity");
        html.Should().Contain("src=\"/js/sw-register.js\"", "the portal reuses the single service-worker registration path");
        html.Should().NotContain("sw-update-toast", "the update prompt was retired in 006");
    }

    /// <summary>
    /// 006: the "Versão de testes" strip and the "Nova versão disponível! · Atualizar · Depois" update
    /// prompt are gone from the shell, its sources, its committed build and the one script it shares
    /// with Blazor. Matched on ASCII fragments because the build escapes non-ASCII text.
    /// </summary>
    [Fact]
    public void ReactShell_CarriesNoTestBannerOrUpdatePrompt()
    {
        var root = FindRepoRoot();
        var web = Path.Combine(root, "src", "RTUB.Web");
        var files = Directory.GetFiles(Path.Combine(web, "portal", "src"))
            .Append(Path.Combine(web, "portal", "index.html"))
            .Concat(Directory.GetFiles(Path.Combine(web, "wwwroot", "portal"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".html") || f.EndsWith(".css") || (f.EndsWith(".js") && !Path.GetFileName(f).StartsWith("vendor-"))))
            .Append(Path.Combine(web, "wwwroot", "js", "sw-register.js"))
            .ToList();

        files.Should().HaveCountGreaterThan(5);
        foreach (var file in files)
        {
            File.ReadAllText(file).Should()
                .NotContain("de testes").And.NotContain("PilotBanner").And.NotContain("pilot__")
                .And.NotContain("Nova vers").And.NotContain("Atualizar").And.NotContain("rtub-sw-")
                .And.NotContain("sw-update-toast", "{0} must not bring back the test banner or the update prompt", Path.GetFileName(file));
        }
    }

    /// <summary>
    /// The shell itself is never cached (above), and every script, stylesheet and font it loads has a
    /// content hash in its name, so a deploy is picked up on the next navigation with no prompt.
    /// </summary>
    [Fact]
    public async Task PortalShell_LoadsOnlyContentHashedAssets()
    {
        var html = await Factory.CreateClient().GetStringAsync("/");

        var assets = Regex.Matches(html, "(?:src|href)=\"(/portal/assets/[^\"]+)\"").Select(m => m.Groups[1].Value).ToList();

        assets.Should().NotBeEmpty().And.OnlyContain(a => Regex.IsMatch(a, @"-[A-Za-z0-9_-]{8}\.(js|css)$"),
            "the React build must stay cache-busted by content hash");
    }

    [Fact]
    public async Task PortalShell_ReferencesOnlyAssetsThatExist()
    {
        var client = Factory.CreateClient();
        var html = await client.GetStringAsync("/");

        var assets = Regex.Matches(html, "(?:src|href)=\"(/portal/assets/[^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        assets.Should().Contain(a => a.EndsWith(".js")).And.Contain(a => a.EndsWith(".css"),
            "wwwroot/portal is the committed build; run `npm run build:portal` after changing portal/");

        foreach (var asset in assets)
        {
            var response = await client.GetAsync(asset);
            response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} is referenced by the portal shell", asset);
        }
    }

    [Fact]
    public void ReactSourcesAndBuild_LinkOnlyToCleanRoutes()
    {
        // A quoted "/portal", "/portal#…" or "/portal/<page>" is a link to the pilot URLs. The build's
        // own asset base ("/portal/" + file, /portal/assets/…) is a static-file folder, not a page.
        var pilotLink = new Regex(@"[""'`(]/portal(?:/(?:privacy|profile|request)\b|[#?""'`)])");
        var root = FindRepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "RTUB.Web", "portal", "src"))
            .Append(Path.Combine(root, "src", "RTUB.Web", "portal", "index.html"))
            .Concat(Directory.GetFiles(Path.Combine(root, "src", "RTUB.Web", "wwwroot", "portal"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".html") || (f.EndsWith(".js") && !Path.GetFileName(f).StartsWith("vendor-"))))
            .ToList();

        files.Should().HaveCountGreaterThan(5);
        foreach (var file in files)
        {
            pilotLink.Matches(File.ReadAllText(file)).Select(m => m.Value)
                .Should().BeEmpty("{0} must link to the clean routes, not /portal", Path.GetFileName(file));
        }
    }

    [Theory]
    [InlineData("/portal", "/")]
    [InlineData("/portal/", "/")]
    [InlineData("/portal/privacy", "/privacy")]
    [InlineData("/portal/profile", "/profile")]
    [InlineData("/portal/request", "/request")]
    [InlineData("/portal/request?utm_source=cartaz", "/request?utm_source=cartaz")]
    public async Task PilotPortalUrls_RedirectTemporarilyToTheCleanRoute(string path, string target)
    {
        var response = await NoRedirectClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "temporary (302) while DEV is hybrid");
        response.Headers.Location!.ToString().Should().Be(target);
    }

    [Fact]
    public async Task PathsOutsideTheReactRoutes_AreNotServedTheShell()
    {
        var client = NoRedirectClient();

        (await client.GetAsync("/portal/unknown")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The Portuguese routes of the first drafts were renamed to English before any release.
        foreach (var old in new[] { "/portal/privacidade", "/portal/perfil", "/portal/pedidos" })
        {
            (await client.GetAsync(old)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was renamed", old);
        }

        foreach (var path in new[] { "/", "/privacy", "/profile", "/portal", "/portal/request", "/music", "/music/albums/1", "/events", "/events/1" })
        {
            (await client.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
                "{0} is GET/HEAD only", path);
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/privacy")]
    [InlineData("/profile")]
    [InlineData("/request")]
    [InlineData("/music")]
    [InlineData("/music/albums/{id:int}")]
    [InlineData("/music/songs/{AlbumId:int}")]
    [InlineData("/login")]
    [InlineData("/roles")]
    [InlineData("/gallery")]
    [InlineData("/events")]
    [InlineData("/events/{id:int}")]
    public void NoBlazorComponent_OwnsAReactRoute(string route)
    {
        var owners = typeof(RTUB.App).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
                .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
                .Select(r => (Type: t, r.Template)))
            .Where(r => r.Template.Equals(route, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Type.FullName)
            .ToList();

        owners.Should().BeEmpty("React owns {0}; its Blazor page was retired", route);
    }

    // ---------- Blazor member/admin bridges ----------

    [Theory]
    [InlineData("/member/events")]
    [InlineData("/member/events?openModal=true&name=x")]
    public async Task BlazorEventManagement_LivesAtMemberEvents_AndRequiresSignIn(string path)
    {
        var response = await NoRedirectClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "event management is for signed-in members only (011)");
        response.Headers.Location!.ToString().Should().Contain("/login").And.Contain("ReturnUrl=%2Fmember%2Fevents");
    }

    /// <summary>
    /// Events use "enrollment" (Inscrições); "attendance" (Presenças) belongs to rehearsals. A member
    /// answers in a modal on the event page: the first 011 build's /events/{id}/enrollment page is
    /// gone and its URL lands on the event with the modal open (?respond=1); the draft
    /// /events/{id}/attendance never reached dev and is not served.
    /// </summary>
    [Fact]
    public async Task EventAnswers_AreAModalOnTheEventPage_NotAPageOfTheirOwn()
    {
        var client = NoRedirectClient();

        var old = await client.GetAsync("/events/12/enrollment");
        old.StatusCode.Should().Be(HttpStatusCode.Redirect);
        old.Headers.Location!.ToString().Should().Be("/events/12?respond=1");
        (await client.PostAsync("/events/12/enrollment", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        // 012E: everyone's answers are the event page's "Quem vai / Quem foi" (and its Admin/Owner manager).
        var list = await client.GetAsync("/events/12/enrollments");
        list.StatusCode.Should().Be(HttpStatusCode.Redirect);
        list.Headers.Location!.ToString().Should().Be("/events/12#who-title");
        (await client.GetAsync("/events/my-enrollments")).StatusCode.Should().Be(HttpStatusCode.OK, "the React shell; the page asks to sign in");
        (await client.GetAsync("/events/1/attendance")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/events/1/attendance")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var root = FindRepoRoot();
        File.ReadAllText(Path.Combine(root, "src", "RTUB.Web", "Pages", "Members", "MemberEvents.razor"))
            .Should().Contain("$\"/events/{eventItem.Id}?respond=1\"", "the bridge's answer buttons open the React answer modal");
        var portal = Path.Combine(root, "src", "RTUB.Web", "portal", "src");
        File.Exists(Path.Combine(portal, "EventEnrollment.tsx")).Should().BeFalse("the standalone answer page was retired");
        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().NotContain("EventEnrollment").And.NotContain("/enrollment'");
        var events = Directory.GetFiles(portal).Where(f => Path.GetFileName(f).StartsWith("Event", StringComparison.Ordinal)
                || f.EndsWith("eventsApi.ts") || f.EndsWith("MyEnrollments.tsx"))
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
        var detail = File.ReadAllText(Path.Combine(src, "EventDetail.tsx"));
        var agenda = File.ReadAllText(Path.Combine(src, "Events.tsx"));
        var manage = File.ReadAllText(Path.Combine(src, "EventManage.tsx"));

        detail.Should().Contain("past ? 'Quem foi' : 'Quem vai'").And.Contain("whoLabel(past)").And.Contain("whoLabel(event.past)");
        detail.Should().NotContain("Gerir atuações").And.NotContain("legacy.memberEvents").And.NotContain("eventEnrollments",
            "the event page sends nobody to the Blazor management pages");

        var card = agenda[agenda.IndexOf("function AgendaCard", StringComparison.Ordinal)..agenda.IndexOf("function ArchiveRow", StringComparison.Ordinal)];
        card.Should().NotContain("Quem vai", "who is going lives on the event page only");
        agenda.Should().Contain("{agenda?.canManage && (").And.Contain("Adicionar atuação")
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
        File.ReadAllText(Path.Combine(src, "EventDialogs.tsx")).Should().Contain("<PrizeManager");
        manage.Should().Contain("eventsApi.addPrize(").And.Contain("eventsApi.renamePrize(").And.Contain("eventsApi.deletePrize(");
        manage.Should().Contain("eventsApi.uploadVideo(").And.Contain("eventsApi.renameVideo(").And.Contain("eventsApi.reorderVideos(")
            .And.Contain("eventsApi.deleteVideo(");
        detail.Should().Contain("<VideoManagerDialog").And.Contain("Gerir vídeos");
        detail.Should().Contain("<RepertoireManagerDialog").And.Contain("Gerir repertório");
        detail.Should().Contain("<ParticipantsManagerDialog").And.Contain("Gerir inscrições").And.Contain("portal.myEnrollments")
            .And.NotContain("legacy.eventEnrollments");
        manage.Should().Contain("eventsApi.addEnrollment(").And.Contain("eventsApi.removeMemberEnrollment(").And.Contain("eventsApi.enrollmentMembers(");
        File.ReadAllText(Path.Combine(src, "main.tsx")).Should().Contain("'/events/my-enrollments'");
        File.ReadAllText(Path.Combine(src, "MyEnrollments.tsx")).Should().Contain("<EnrollmentDialog").And.NotContain("presen");
        manage.Should().Contain("eventsApi.addToRepertoire(").And.Contain("eventsApi.removeFromRepertoire(")
            .And.Contain("eventsApi.reorderRepertoire(").And.Contain("eventsApi.clearRepertoireDay(").And.Contain("eventsApi.repertoireSongs(");
    }

    [Fact]
    public void MemberEvents_KeepsOnlyTheBridgeTools_EverythingElseIsReact()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "RTUB.Web", "Pages", "Members", "MemberEvents.razor"));

        // 012A: one way to do each of these, the React agenda.
        page.Should().NotContainAny(new[]
        {
            "ImageUploadManager", "ImageCropper", "UploadImageAsync", "UpdateEventWithImageAsync",
            "OnSendEmail=", "OnSendPushNotification=", "OnCancelEvent=", "OnUncancelEvent=",
            "CancelEventAsync", "UncancelEventAsync", "SendEventNotificationAsync", "SendEventReminderNotificationAsync",
            "SendEventCancellationNotificationAsync", "SendToSelectedUsersAsync",
            // 012B: prizes are managed in the React Prémios modal.
            "OpenTrophyModal", "OpenCreateTrophyModal", "SaveTrophy", "DeleteTrophy", "TrophyService", "OnViewTrophies=", "ShowTrophy=",
            // 012C: videos are managed on the React event page; the bridge only plays them.
            "InputFile", "AddVideoAsync", "UpdateVideoTitleAsync", "UpdateVideoOrderAsync", "DeleteVideoAsync", "draggable", "EditVideo",
            // 012D: the repertoire is managed on the React event page.
            "OnRepertoireChanged", "HandleRepertoireChanged",
            // 012E: participants and Minhas Inscrições are React (event page, /events/my-enrollments).
            "MyEnrollmentsButton", "Minhas Inscrições", "OnViewEnrollments=", "ModalType.EnrollmentList", "AddMemberToEvent",
            "DeleteEnrollmentAsync", "/enrollments",
        });

        // 012D: the shared repertoire modal (used only here) is read-only.
        var repertoire = File.ReadAllText(Path.Combine(root, "src", "RTUB.Shared", "Components", "Modals", "RepertoireModal.razor"));
        repertoire.Should().NotContainAny(new[]
        {
            "AddSongToRepertoireAsync", "RemoveSongFromRepertoireAsync", "UpdateRepertoireOrderAsync", "RemoveRepertoireDayAsync",
            "draggable", "IsAdmin", "ISongService",
        });

        // What stays: read-only videos and repertoire, statistics (incl. prizes), discussion, the details-only edit.
        page.Should().Contain("ModalType.Videos").And.Contain("<RepertoireModal").And.Contain("TrophiesStats")
            .And.Contain("<EnrollmentStatisticsButton").And.Contain("NavigateToDiscussion").And.Contain("ModalType.Edit");

        // 012E: the Blazor participants page and the Minhas Inscrições component are gone.
        File.Exists(Path.Combine(root, "src", "RTUB.Web", "Pages", "Activities", "EventEnrollments.razor")).Should().BeFalse();
        File.Exists(Path.Combine(root, "src", "RTUB.Shared", "Components", "UI", "MyEnrollmentsButton.razor")).Should().BeFalse();
    }

    [Fact]
    public void EventsLinks_GoToTheReactAgenda_AndManagementToMemberEvents()
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src", "RTUB.Web", "portal", "src");

        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("events: '/events'").And.Contain("memberEvents: '/member/events'");
        File.ReadAllText(Path.Combine(src, "Home.tsx")).Should().Contain("<MoreLink href={portal.events}>",
            "the home agenda's call to action opens the React agenda");
        File.ReadAllText(Path.Combine(src, "App.tsx")).Should().Contain("events: portal.events,",
            "the top bar, the mobile menu and the footer open the agenda page, not the home section");
        File.ReadAllText(Path.Combine(root, "src", "RTUB.Web", "Pages", "Management", "Requests.razor"))
            .Should().Contain("\"/member/events\"", "turning a request into an event opens the management page");
        File.ReadAllText(Path.Combine(root, "src", "RTUB.Web", "Shared", "MainLayout.razor"))
            .Should().Contain("href=\"/events\" data-enhance-nav=\"false\"", "the Blazor menu opens the React agenda with a full load");
    }

    [Fact]
    public async Task BlazorMemberProfile_LivesAtMemberProfile_AndStillRequiresSignIn()
    {
        var response = await NoRedirectClient().GetAsync("/member/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login").And.Contain("ReturnUrl=%2Fmember%2Fprofile");
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

        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("music: '/music'");
        File.ReadAllText(Path.Combine(src, "Home.tsx")).Should().Contain("<MoreLink href={portal.music}>");
        File.ReadAllText(Path.Combine(src, "App.tsx")).Should().Contain("music: portal.music,",
            "the top bar, the mobile menu and the footer open the Music page, not the home section");
        Directory.GetFiles(src).Select(File.ReadAllText).Should().NotContain(t => Regex.IsMatch(t, "(?<!/api)/music/songs/"),
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
