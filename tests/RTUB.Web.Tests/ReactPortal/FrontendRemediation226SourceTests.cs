using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// #226 batch 2 (frontend, static assets, service worker): the contracts the cleanup must keep. Source-level, like the
/// other portal guards: the built bundle is committed, so the chunk layout is checked on the real output too.
/// </summary>
public class FrontendRemediation226SourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Web = Path.Combine(Root, "src", "RTUB.Web");
    private static readonly string Src = Path.Combine(Web, "portal", "src");
    private static readonly string Assets = Path.Combine(Web, "wwwroot", "portal", "assets");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file)).Replace("\r\n", "\n");
    private static string Css() => PortalCss.Read(Src).Replace("\r\n", "\n");

    [Fact]
    public void EventManagement_LoadsOnlyWhenADialogOpens()
    {
        // Task 113: components/EventDialogs.tsx was split into EventAnswer, EventPrizesDialog and EventStats.
        foreach (var page in new[] { "pages/Events.tsx", "pages/EventDetail.tsx", "components/EventAnswer.tsx+components/EventPrizesDialog.tsx+components/EventStats.tsx" })
        {
            var prefix = page.StartsWith("pages/", StringComparison.Ordinal) ? "../components/" : "./";
            string.Join("\n", page.Split('+').Select(Read)).Should().Contain($"from '{prefix}EventManageLazy'").And.NotContain($"from '{prefix}EventManage'", "{0} must not pull the admin chunk", page);
        }

        Read("components/EventManageLazy.tsx").Should().Contain("const load = () => import('./EventManage');").And.Contain("<Suspense fallback=");
        Directory.GetFiles(Assets, "EventManage-*.js").Should().ContainSingle("the admin dialogs are their own chunk");
        // Task 113: EventManageLazy now sits in the EventsCards chunk that the Events chunk imports, so both are read.
        var events = File.ReadAllText(Directory.GetFiles(Assets, "Events-*.js").Single()) + "\n" + File.ReadAllText(Directory.GetFiles(Assets, "EventsCards-*.js").Single());
        events.Should().NotContain("A imagem atual é apagada ao guardar.", "no EventManage text in the Events chunk").And.Contain("EventManage-");
    }

    [Fact]
    public void Searches_LetTheLatestAnswerWin()
    {
        var app = Read("components/AppShared.tsx");
        app.Should().Contain("export function useLatest()").And.Contain("if (id === last.current) apply(value);").And.Contain("last.current = -1;");

        foreach (var page in new[] { "pages/Inventory.tsx", "pages/Leaderboard.tsx", "pages/Logistics.tsx", "pages/Requests.tsx", "pages/Shop.tsx", "pages/Gallery.tsx" })
        {
            Read(page).Should().Contain("const latest = useLatest();", page);
        }

        // Task 113: the pickers moved out of LogisticsDialogs, EventDiscussion, EventManage (two), GalleryManage, MemberManage,
        // RehearsalDetail, Questions and MemberDialogs into the files below.
        foreach (var picker in new[] { "components/LogisticsFields.tsx", "components/TreasuryUi.tsx", "components/EventDiscussionLift.tsx", "components/EventManageParticipants.tsx",
                     "components/EventManageRepertoire.tsx", "components/GovernanceManage.tsx", "components/GalleryFormParts.tsx", "components/MemberFormFields.tsx",
                     "components/RehearsalAttendees.tsx", "pages/Members.tsx", "pages/Naipes.tsx", "components/QuestionList.tsx", "components/MemberListDialogs.tsx" })
        {
            var source = Read(picker);
            source.Should().Contain("let live = true;", picker).And.Contain("live = false;", picker);
        }
    }

    [Fact]
    public void ServiceWorker_PrunesOlderBuildsOfAChunk_AndOnlyOnAFirstStore()
    {
        var worker = File.ReadAllText(Path.Combine(Web, "wwwroot", "service-worker.js")).Replace("\r\n", "\n");
        worker.Should().Contain("const CACHE_VERSION = 'rtub-v2.12.0';");
        worker.Should().Contain(@"const HASHED_PORTAL_ASSET = /^\/portal\/assets\/(.+)-[A-Za-z0-9_-]{8}\.(js|css)$/;");
        worker.Should().Contain("if (!cached) {").And.Contain("stored.then(() => pruneOlderBuilds(cache, url))");
        worker.Should().Contain("match[1] === current[1] && match[2] === current[2]", "only the same chunk name and type is pruned");
        worker.Should().Contain("const NEVER_CACHE_PREFIXES = ['/api/', '/auth/', '/hubs/', '/health'];");

        // Pruning by name is safe because one build never has two chunks with the same name and type.
        var hashed = new Regex(@"^(.+)-[A-Za-z0-9_-]{8}\.(js|css)$");
        var names = Directory.GetFiles(Assets).Select(Path.GetFileName).Select(f => hashed.Match(f!)).Where(m => m.Success)
            .Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}").ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void MemberDetails_ShowTheProfileTimeline()
    {
        var dialogs = Read("components/MemberDialogs.tsx");
        dialogs.Should().Contain("export function TunaPath(").And.Contain("<TunaPath items={m.timeline} />").And.NotContain("member-timeline");
        Read("components/ProfileTuna.tsx").Should().Contain("<TunaPath items={m.timeline} />").And.Contain("TunaPath, progressText } from './MemberDialogs'");
        Regex.Match(Css(), @"\n\.member-path \{[^}]*\}").Value.Should().Contain("min-width: 0;",
            "the timeline scrolls inside the dialog, never the page");
    }

    [Fact]
    public void OldIconCopies_AreGone_AndTheBrandIconsAreLight()
    {
        Directory.Exists(Path.Combine(Web, "wwwroot", "images", "icons")).Should().BeFalse();
        new FileInfo(Path.Combine(Web, "wwwroot", "icons", "rtub-logo-512.png")).Length.Should().BeLessThan(128 * 1024);
        new FileInfo(Path.Combine(Web, "wwwroot", "icons", "rtub-logo-192.png")).Length.Should().BeLessThan(32 * 1024);
        File.ReadAllText(Path.Combine(Root, "src", "RTUB.Application", "Services", "MessagingService.cs"))
            .Should().Contain("public const string GroupAvatar = \"/icons/rtub-logo-352.webp\";");
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root directory");
    }
}
