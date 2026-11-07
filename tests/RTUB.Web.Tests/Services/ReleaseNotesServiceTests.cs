using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Web.Tests.Services;

/// <summary>
/// Task 083 (#264): which bundled notes a build may show, without a database. Version order is SemVer (2.1.10 after
/// 2.1.9), a DEV build hides its notes unless told otherwise, a newer file than the build is never shown (a rollback
/// restores its own notes, never the newer release's), and an invalid file is ignored, not fatal.
/// </summary>
public sealed class ReleaseNotesServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rtub-notes-" + Guid.NewGuid().ToString("N"));

    public ReleaseNotesServiceTests()
    {
        Directory.CreateDirectory(_dir);
        Write("2.1.9", "Nova atualização 2.1.9", "a");
        Write("2.1.10", "Nova atualização 2.1.10", "b");
        Write("2.2.0", "Nova atualização 2.2.0", "c");
        File.WriteAllText(Path.Combine(_dir, "2.1.8.json"), "{not json");
        File.WriteAllText(Path.Combine(_dir, "2.1.7.json"), """{"version":"2.1.6","title":"wrong name","items":["x"]}""");
    }

    private ReleaseNotesService Service(string build, bool showPrerelease = false) => new(
        Mock.Of<IDbContextFactory<ApplicationDbContext>>(), Options.Create(new ReleaseNotesOptions { Directory = _dir, ShowPrerelease = showPrerelease }),
        Mock.Of<ILogger<ReleaseNotesService>>(), new BuildInfo(build, "abc"));

    [Fact]
    public void Bundled_IsNewestFirst_BySemVer_NotNewerThanTheBuild()
    {
        Service("2.1.10").Bundled.Select(n => n.Version).Should().Equal("2.1.10", "2.1.9");
        Service("2.2.0").Bundled.Select(n => n.Version).Should().Equal("2.2.0", "2.1.10", "2.1.9");
        Service("2.1.9").Bundled.Select(n => n.Version).Should().Equal(new[] { "2.1.9" }, "a rolled-back build shows only its own and older notes");
        Service("3.0.0").Bundled.Select(n => n.Version).Should().Equal(new[] { "2.2.0", "2.1.10", "2.1.9" }, "a release without a file of its own still lists the older ones");
    }

    [Fact]
    public void PrereleaseBuild_HidesTheNotes_UnlessShowPrereleaseIsOn()
    {
        Service("2.2.0-dev.17").ReleaseVersion.Should().BeNull();
        Service("2.2.0-dev.17").Bundled.Should().BeEmpty();
        Service("2.2.0-dev.17", showPrerelease: true).ReleaseVersion.Should().Be("2.2.0");
        Service("2.2.0-dev.17", showPrerelease: true).Bundled.Select(n => n.Version).Should().Equal("2.2.0", "2.1.10", "2.1.9");
        Service("unknown").ReleaseVersion.Should().BeNull();
    }

    [Fact]
    public void InvalidFiles_AreIgnored_AndTheRepositorysOwnNotesLoad()
    {
        Service("2.2.0").Bundled.Should().NotContain(n => n.Version == "2.1.8" || n.Version == "2.1.7" || n.Version == "2.1.6");

        // The repository's own files load, every one is at or below dev's VERSION (notes for the current version are
        // optional since 090: a release without a file shows no dialog).
        var root = FindRoot();
        var current = File.ReadAllText(Path.Combine(root, "VERSION")).Trim();
        var real = new ReleaseNotesService(Mock.Of<IDbContextFactory<ApplicationDbContext>>(),
            Options.Create(new ReleaseNotesOptions { Directory = Path.Combine(root, "releases") }), Mock.Of<ILogger<ReleaseNotesService>>(), new BuildInfo(current, null));
        var files = Directory.GetFiles(Path.Combine(root, "releases"), "*.json").Select(Path.GetFileNameWithoutExtension).ToList();
        real.Bundled.Select(n => n.Version).Should().BeEquivalentTo(files, "every committed notes file is valid and not newer than VERSION");
        real.Bundled.Should().OnlyContain(n => ReleaseNotesService.Compare(n.Version, current) <= 0 && n.Items.Count > 0);
    }

    [Theory]
    [InlineData("2.1.9", "2.1.10", -1)]
    [InlineData("2.1.10", "2.1.9", 1)]
    [InlineData("2.10.0", "2.9.9", 1)]
    [InlineData("2.3.4", "2.3.4", 0)]
    public void Compare_IsNumeric(string a, string b, int sign) => Math.Sign(ReleaseNotesService.Compare(a, b)).Should().Be(sign);

    private void Write(string version, string title, string item) =>
        File.WriteAllText(Path.Combine(_dir, $"{version}.json"), $$"""{"version":"{{version}}","title":"{{title}}","items":["{{item}}"]}""");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RTUB.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
