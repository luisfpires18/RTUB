using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 045 performance gate: the fixes that took mobile Lighthouse from 61-80 to 94-98 (docs/performance-gate.md).
/// The splash and the header logo are a 352px WebP (the 465 KB 512px PNG competed with the JS on every cold load), the
/// layout does not shift when a page's data arrives, and the logistics board lists its R2 folder only on open and after
/// a file change.
/// </summary>
public class PerformanceGateSourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Portal = Path.Combine(Root, "src", "RTUB.Web", "portal");

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([Portal, .. path])).Replace("\r\n", "\n");

    [Fact]
    public void SplashAndHeaderLogo_AreTheSmallWebP()
    {
        var webp = new FileInfo(Path.Combine(Root, "src", "RTUB.Web", "wwwroot", "icons", "rtub-logo-352.webp"));
        webp.Exists.Should().BeTrue();
        webp.Length.Should().BeLessThan(64 * 1024);

        Read("index.html").Should().Contain("class=\"splash__logo\" src=\"/icons/rtub-logo-352.webp\"").And.NotContain("rtub-logo-512.png");
        Read("src", "App.tsx").Should().Contain("<img className=\"brand__logo\" src=\"/icons/rtub-logo-352.webp\"");
        Read("src", "Home.tsx").Should().Contain("<source media=\"(max-width: 599px)\" srcSet=\"/icons/rtub-logo-352.webp\" />",
            "the hero emblem is hidden on phones, and a hidden <img> still downloads");
    }

    [Fact]
    public void NothingShiftsWhenThePageArrives()
    {
        var css = Read("src", "styles.css");
        Regex.Match(css, @"\n\.boot \{[^}]*\}").Value.Should().Contain("margin: 0 auto;").And.NotContain("margin: 96px",
            "a top margin collapses through <body>, which then jumps when React replaces the fallback");
        Regex.Match(css, @"\nmain \{[^}]*\}").Value.Should().Contain("min-height: calc(100vh - var(--header-h));",
            "the footer starts below the fold instead of jumping under the loader");
        Read("src", "Members.tsx").Should().Contain("{directory !== 'signin' && directory !== null && (\n          <div className=\"events-page__actions\">");
    }

    [Fact]
    public void LogisticsBoard_ListsItsFilesOnOpenAndAfterAFileChangeOnly()
    {
        var board = Read("src", "LogisticsBoard.tsx");
        var load = Regex.Match(board, @"\n  const load = \(\) =>.*?\n\n", RegexOptions.Singleline).Value;
        load.Should().Contain("logisticsApi.board(boardId)").And.NotContain("fileList", "every card / list change reloads the board, never the R2 listing");
        Regex.Matches(board, @"loadFiles\(\)").Count.Should().Be(2, "on open and on 'Tentar novamente'");
        board.Should().Contain("<FilesDialog board={data} onClose={() => setOpen(undefined)} onChanged={loadFiles} />");
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
