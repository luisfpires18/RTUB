using FluentAssertions;

namespace RTUB.Integration.Tests.Api;
/// <summary>The React profile, members map and Hall of Fame pages and the member menu. What is gone is in RetiredRoutesTests.</summary>
public class MemberAreaPagesTests
{

    [Fact]
    public void ReactPages_AreLinked_AndSayNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var content = File.ReadAllText(Path.Combine(portal, "data/content.ts"));
        content.Should().Contain("membersMap: '/members/map'").And.Contain("hallOfFame: '/hall-of-fame'").And.NotContain("memberProfile");
        File.ReadAllText(Path.Combine(portal, "components/MemberShell.tsx")).Should().Contain("href: portal.membersMap,").And.Contain("href: portal.hallOfFame,");
        File.ReadAllText(Path.Combine(portal, "pages/Members.tsx")).Should().Contain("href={portal.membersMap}");
        File.ReadAllText(Path.Combine(portal, "pages/Leaderboard.tsx")).Should().NotContain("portal.hallOfFame", "Classificação and Hall of Fame are not presented as one feature");
        var main = File.ReadAllText(Path.Combine(portal, "main.tsx"));
        main.Should().Contain("'/members/map': lazy(").And.Contain("'/hall-of-fame': lazy(").And.Contain("'/profile': lazy(");

        foreach (var file in new[] { "pages/Profile.tsx", "pages/MembersMap.tsx", "pages/HallOfFame.tsx", "data/memberAreaApi.ts" })
        {
            File.ReadAllText(Path.Combine(portal, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }

        // Push came back with task 038 as its own section (PushV2SourceTests), not as the old toggle.
        File.ReadAllText(Path.Combine(portal, "pages/Profile.tsx")).Should().Contain("<PushSection />").And.NotContain("PushNotificationToggle");
    }

    [Fact]
    public void ProfilePage_Batch3_HeaderPhotoTunaInfoAndSharedBadge()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        // 113: /profile is the page plus its Profile* parts.
        var profile = PortalSource.Cat(portal, "pages/Profile.tsx", "components/Profile*.tsx");

        profile.Should().NotContain("Entraste como", "one account per member: the sentence says nothing");
        profile.Should().NotContain("<PhotoSection", "the photo lives in the header, not in a card of its own")
            .And.Contain("<ProfilePhoto").And.Contain("memberAreaApi.photo(");
        profile.Should().Contain("title=\"Informação da Tuna\"").And.Contain("Percurso na Tuna").And.Contain("Estado na Tuna")
            .And.Contain("id=\"tuna-instruments-title\"").And.Contain("id=\"tuna-mentor-title\"")
            .And.Contain("profile.instrumentPictures[i.instrument]").And.Contain("t.mentorAvatarUrl");
        profile.Should().Contain("<TunaPath items={m.timeline} />", "the timeline is the server's real member data, never hardcoded")
            .And.Contain("Último ensaio").And.Contain("Última atuação");
        profile.Should().NotContain("portal.myEnrollments").And.Contain("<SignOutButton").And.Contain("buttonClassName=\"btn btn--ghost btn--sm\"");

        // One badge component, with the per-kind variants kept in it: profile header, dialogs, directory and the header badge.
        File.ReadAllText(Path.Combine(portal, "components/MemberBadge.tsx")).Should().Contain("member-badge--${kind}");
        // The directory rows moved from pages/Members.tsx to MemberGroup.tsx in 113; the profile header is Profile.tsx + ProfileTuna.tsx.
        foreach (var file in new[] { "pages/Profile.tsx", "components/MemberDialogs.tsx", "components/MemberGroup.tsx", "components/MemberShell.tsx" })
        {
            File.ReadAllText(Path.Combine(portal, file)).Should().Contain("<MemberBadge", file);
        }

        var css = PortalCss.Read(portal);
        css.Should().Contain(".member-badge--tuno").And.Contain(".member-badge--veterano").And.Contain(".member-badge--tunossauro")
            .And.NotContain(".profile-photo", "the standalone Foto card is gone");

        // Sweep: no page draws its own member badge. Only MemberBadge.tsx writes the class; the old local pill classes are gone.
        foreach (var file in Directory.GetFiles(portal, "*.tsx", SearchOption.AllDirectories).Where(f => !f.EndsWith("MemberBadge.tsx", StringComparison.Ordinal)))
        {
            File.ReadAllText(file).Should().NotContainAny(new[] { "className=\"member-badge\"", "className=\"member-badge ", "'member-badge ", "who__badge", "talk__badge", "owner-badge--danger" }, Path.GetFileName(file));
        }

        // Header (batch 4): inbox, then the category badge as its own control, then the profile chip, then the menu.
        var app = File.ReadAllText(Path.Combine(portal, "components/AppChrome.tsx")); // the header moved out of App.tsx in 113
        app.IndexOf("<MemberInbox />", StringComparison.Ordinal).Should().BeInRange(0, app.Length)
            .And.BeLessThan(app.IndexOf("<MemberCategory", StringComparison.Ordinal));
        app.IndexOf("<MemberCategory", StringComparison.Ordinal).Should().BeLessThan(app.IndexOf("<MemberIdentity", StringComparison.Ordinal));
        app.IndexOf("<MemberIdentity", StringComparison.Ordinal).Should().BeLessThan(app.IndexOf("<MemberDrawer", StringComparison.Ordinal));
        var shell = File.ReadAllText(Path.Combine(portal, "components/MemberShell.tsx"));
        var chip = shell[shell.IndexOf("export function MemberIdentity", StringComparison.Ordinal)..shell.IndexOf("export function SignOutButton", StringComparison.Ordinal)];
        chip.Should().NotContain("MemberBadge", "the badge is no longer inside the profile chip").And.Contain("href={portal.profile}");

        // The timeline exposes semantic variants (leitao, caloiro, position, tuno, veterano, tunossauro) from the server's accent.
        // Since #226 batch 2 the component lives in MemberDialogs.tsx, shared by /profile and the member details.
        var dialogs = File.ReadAllText(Path.Combine(portal, "components/MemberDialogs.tsx"));
        dialogs.Should().Contain("tuna-path__step--${pathVariant(t)}").And.Contain("'position'");
        foreach (var variant in new[] { "leitao", "caloiro", "position", "tuno", "veterano", "tunossauro" })
        {
            css.Should().Contain($".tuna-path__step--{variant}");
        }
    }

    [Fact]
    public void EventMemberPanel_KeepsDiscussionAndContacts_AndSignedInPagesShareOneWidth()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var detail = File.ReadAllText(Path.Combine(portal, "components/EventDetailBody.tsx")); // the member panel moved out of the page in 113
        var panel = detail[detail.IndexOf("member-panel__links", StringComparison.Ordinal)..];
        panel = panel[..panel.IndexOf("</aside>", StringComparison.Ordinal)];
        panel.Should().Contain("portal.eventDiscussion(").And.Contain("portal.eventContacts(")
            .And.NotContain("whoLabel").And.NotContain("portal.myEnrollments");

        File.ReadAllText(Path.Combine(portal, "pages/EventContacts.tsx")).Should().NotContain("Mod e acima</p>", "the eyebrow label is gone");

        // One container: the signed-in shell sets the width, no page narrows itself with its own max-width.
        var css = PortalCss.Read(portal);
        css.Should().Contain(".shell--member {");
        System.Text.RegularExpressions.Regex.IsMatch(css, @"\.(talk|contacts)-page\s*\{\s*max-width").Should()
            .BeFalse("Discussão and Contactos used to be narrow centred columns");
    }

    [Fact]
    public void Messages_OnPhones_IsAnAppScreen_WithoutTheSiteFooter()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal");
        File.ReadAllText(Path.Combine(portal, "src", "pages/Messages.tsx")).Should().Contain("classList.add('app-screen')").And.Contain("classList.remove('app-screen')");
        var css = PortalCss.Read(Path.Combine(portal, "src"));
        css.Should().Contain("html.app-screen .footer").And.Contain("100dvh", "dynamic viewport units, not 100vh");
        File.ReadAllText(Path.Combine(portal, "index.html")).Should().Contain("interactive-widget=resizes-content");
    }

    [Fact]
    public void Messages_FillTheViewportOnDesktop_AndSystemWearsThePortalLogo()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var messages = PortalSource.Cat(src, "pages/Messages.tsx", "components/Message*.tsx", "data/messagesApi.ts");
        messages.Should().Contain("/icons/rtub-logo-352.webp", "the logo the portal header uses").And.Contain("kind === 'system' ? RTUB_LOGO : avatarUrl", "only Notificações RTUB wears the portal logo; groups a chat icon, people their own avatar")
            .And.Contain("system: 0, group: 1, direct: 2", "Sistema RTUB, then groups, then people");
        PortalSource.Cat(src, "components/App*.tsx").Should().Contain("/icons/rtub-logo-352.webp");

        // The app-screen rules are not phone-only: the desktop shell fits the viewport too, scoped to /messages by html.app-screen.
        var css = PortalCss.Read(src);
        var screen = css.IndexOf("html.app-screen,", StringComparison.Ordinal);
        screen.Should().BeGreaterThan(0);
        css.LastIndexOf("@media (max-width: 899px)", screen, StringComparison.Ordinal).Should().BeLessThan(css.LastIndexOf("@media (max-width: 899px)", StringComparison.Ordinal),
            "the base app-screen rules come before the phone-only block");
        css.Should().Contain("grid-template-columns: minmax(260px, 30%) minmax(0, 1fr)");
    }

    [Fact]
    public void MultiPagePolish_MenuLabelsAndDecoupling()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        string R(string f) => File.ReadAllText(Path.Combine(src, f));

        // Menu: no "Recursos" group, no "As minhas inscrições"; Documentação once under Gestão; Instrumentos once under Tuna.
        var shell = R("components/MemberShell.tsx");
        shell.Should().NotContain("'Recursos'").And.NotContain("As minhas inscrições").And.NotContain("key: 'resources'");
        var management = shell[shell.IndexOf("key: 'management'", StringComparison.Ordinal)..shell.IndexOf("key: 'owner'", StringComparison.Ordinal)];
        management.Should().Contain("portal.documentation").And.NotContain("portal.inventory");
        var tuna = shell[shell.IndexOf("key: 'tuna'", StringComparison.Ordinal)..shell.IndexOf("key: 'management'", StringComparison.Ordinal)];
        tuna.Should().Contain("portal.inventory").And.Contain("label: 'Instrumentos'");
        System.Text.RegularExpressions.Regex.Matches(shell, @"portal\.documentation,").Count.Should().Be(1);
        System.Text.RegularExpressions.Regex.Matches(shell, @"portal\.inventory,").Count.Should().Be(1);

        string Events() => PortalSource.Cat(src, "pages/Events.tsx", "components/EventsCards.tsx", "components/EventsToolbar.tsx");
        Events().Should().Contain("Onde estiveste").And.Contain("<WhereYouWereDialog").And.Contain("btn--prizes").And.NotContain("btn--gold");
        PortalSource.Cat(src, "pages/EventDetail.tsx", "components/EventDetail*.tsx").Should().Contain("Área da atuação").And.NotContain("Para membros").And.Contain("btn--prizes");
        R("pages/Rehearsals.tsx").Should().Contain("Adicionar vários").And.NotContain("Vários");
        R("pages/HallOfFame.tsx").Should().NotContain("portal.leaderboard");
        Events().Should().NotContain("As minhas inscrições", "the button says Onde estiveste");
        R("components/EventStats.tsx").Should().Contain("title=\"Onde estiveste\"");
        System.Text.RegularExpressions.Regex.IsMatch(R("pages/Naipes.tsx"), @"btn--primary btn--sm""\s+onClick=\{\(\) => setEditing\(\{ kind: 'new', isVideo: false \}\)\}").Should().BeTrue("Adicionar imagem is a primary action");
        System.Text.RegularExpressions.Regex.IsMatch(PortalCss.Read(src), @"\.lx-board-page\s*\{\s*max-width").Should().BeFalse("the board uses the shared container");
        R("components/MemberDialogs.tsx").Should().Contain("export const progressText");
        System.Text.RegularExpressions.Regex.IsMatch(PortalCss.Read(src), @"\.naipe-picker\s*\{[^}]*justify-content:\s*center").Should().BeTrue("the instrument picker is centred");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
