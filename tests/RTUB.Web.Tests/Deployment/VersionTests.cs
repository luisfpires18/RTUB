using System.Text;
using FluentAssertions;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Web.Tests.Deployment;

/// <summary>
/// The root VERSION file is the release version: what "deploy 2.0.1" and "roll back to 2.0.0" name.
/// The release scripts validate it byte for byte; this pins the same contract in the ordinary test
/// run, plus the parser behind GET /api/version.
/// </summary>
public class VersionTests
{
    [Fact]
    public void VersionFile_IsStrictSemVer_WithNothingElseInIt()
    {
        var bytes = File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), "VERSION"));
        var text = Encoding.ASCII.GetString(bytes);

        bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, "no BOM");
        text.Should().MatchRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\n?$",
            "MAJOR.MINOR.PATCH, optionally one LF - no CR, spaces, prerelease or build metadata");
    }

    [Fact]
    public void RunningAssembly_ReportsTheVersionFile()
    {
        var expected = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "VERSION")).Trim();

        BuildInfo.Current.Version.Should().Be(expected, "Directory.Build.props stamps every build from VERSION");
    }

    [Theory]
    [InlineData("1.1.2+0123456789abcdef0123456789abcdef01234567", "1.1.2", "0123456789abcdef0123456789abcdef01234567")]
    [InlineData("1.1.2-dev.41+abc", "1.1.2-dev.41", "abc")]
    [InlineData("1.1.2", "1.1.2", null)]
    [InlineData("", "unknown", null)]
    [InlineData(null, "unknown", null)]
    public void Parse_SplitsVersionAndCommitAtThePlus(string? informational, string version, string? commit)
    {
        BuildInfo.Parse(informational).Should().Be(new BuildInfo(version, commit));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "VERSION")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
