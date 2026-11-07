using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 080 (#261): the startup "Ativa as notificações" reminder. Members only, one dialog at a time after the page's own
/// dialogs, the browser asked only from the Ativar click, and a launch flag per tab session that the visitor shell
/// clears. The device and account rules are checked by `npm run check:startup` (portal/checks/startup.check.mjs).
/// </summary>
public class StartupReminderPortalTests
{
    private static readonly string Src = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file));

    [Fact]
    public void TheReminder_IsForMembers_NeverStacks_AndAsksTheBrowserOnlyFromTheClick()
    {
        var app = Read("App.tsx");
        var startup = Read("Startup.tsx");

        app.Should().Contain("{member && <StartupModals />}", "visitors never see it");
        app.Should().Contain("if (user && !user.authenticated) resetStartup();", "sign-out and /login start a new launch");
        startup.Should().Contain("sessionStorage").And.NotContain("localStorage", "an opening is the tab session, not forever");
        startup.Should().Contain("const OTHER_DIALOG = 'dialog[open]:not(.startup-modal)';").And.Contain("new MutationObserver(update)")
            .And.Contain("if (!state || hidden) return null;", "hidden while another dialog is open, even one that opens later");
        startup.Should().Contain("'standalone' in navigator ?", "iPhone / iPad install steps only where that route exists");
        startup.Should().NotContain("requestPermission", "only turnOn, from the Ativar click, asks the browser");
        startup.Should().Contain("const result = await turnOn(state.status.vapidPublicKey);");
        startup.Should().Contain("const STEPS: Step[] = [ReleaseNotes, PushReminder];", "release notes (083) first, then the reminder");
        startup.Should().Contain("Ativa as notificações").And.Contain("A app RTUB é agora a principal fonte de informação da tuna.")
            .And.Contain("Recebe os avisos de atuações, ensaios e ações pendentes.").And.Contain("Ativar notificações").And.Contain("Agora não");
        startup.Should().Contain("if (account.kind !== 'ok' || device.kind === 'error') return onDone();", "unknown is not off");
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
