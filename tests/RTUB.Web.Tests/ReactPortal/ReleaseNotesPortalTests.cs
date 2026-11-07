using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 083 (#264): the "Nova atualização" dialog. Members only (it rides the startup queue, first), the server decides
/// what is pending, Entendido is the only acknowledgement and a failed receipt is never claimed as saved; the footer
/// reopens the running release's notes without touching the receipt; the strings are rendered as text, never HTML.
/// </summary>
public class ReleaseNotesPortalTests
{
    private static readonly string Src = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file));

    [Fact]
    public void TheDialog_RunsFirstInTheStartupQueue_AcknowledgesOnlyOnEntendido_AndNeverClaimsAFailedReceipt()
    {
        var startup = Read("Startup.tsx");
        var api = Read("releaseNotesApi.ts");

        startup.Should().Contain("const STEPS: Step[] = [ReleaseNotes, PushReminder];");
        startup.Should().Contain("if (o.data.pending.length === 0) {", "the server decides what is pending");
        startup.Should().Contain("const o = await releaseNotesApi.acknowledge(version);");
        startup.Should().Contain("if (!notes || hidden) return null;", "never stacked on another dialog");
        startup.Should().Contain("className=\"startup-modal\"");
        startup.Should().Contain("Não foi possível guardar que viste as novidades.", "a failed receipt is shown, not swallowed");
        // Reading the state never acknowledges: the only POST is from Entendido.
        startup.Should().NotContain("localStorage");
        api.Should().Contain("'/api/releases/notes'").And.Contain("'/api/releases/notes/ack'").And.NotContain("localStorage");
    }

    [Fact]
    public void TheFooter_ReopensTheRunningReleasesNotes_WithoutAcknowledging()
    {
        var app = Read("App.tsx");
        var footer = app[app.IndexOf("function VersionTag", StringComparison.Ordinal)..];

        footer.Should().Contain("Ver novidades da versão").And.Contain("const member = user?.authenticated === true;")
            .And.Contain("releaseNotesApi.state()").And.NotContain("acknowledge");
        footer.Should().Contain("Esta versão não tem novidades para mostrar.").And.Contain("Não foi possível ver as novidades.");
    }

    [Fact]
    public void TheNotes_AreRenderedAsText_OneSectionPerRelease_NewestFirst()
    {
        var dialog = Read("ReleaseNotes.tsx");

        dialog.Should().NotContain("dangerouslySetInnerHTML").And.NotContain("innerHTML");
        dialog.Should().Contain("const [first, ...older] = notes;").And.Contain("Desde a última vez que viste as novidades:")
            .And.Contain("Entendido").And.Contain("<Dialog");
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
