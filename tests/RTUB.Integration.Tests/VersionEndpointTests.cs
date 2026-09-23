using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// <c>GET /api/version</c> is what every deploy and rollback smoke test polls to prove the build it
/// just deployed is the one answering. It must be anonymous, uncached, and carry the repository's
/// VERSION plus the full commit SHA - and nothing else.
/// </summary>
public class VersionEndpointTests : IntegrationTestBase
{
    public VersionEndpointTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Version_IsAnonymousUncachedAndCarriesVersionAndCommitOnly()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.CacheControl!.NoStore.Should().BeTrue("a cached answer would let smoke pass against an old build");
        response.Headers.Contains("Content-Security-Policy").Should().BeFalse("CSP is for HTML documents only");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("version", "commit");
        json.RootElement.GetProperty("version").GetString().Should().Be(RepositoryVersion());
        json.RootElement.GetProperty("commit").GetString().Should().MatchRegex("^[0-9a-f]{40}$",
            "the SDK appends the full commit SHA; smoke compares it verbatim");
    }

    private static string RepositoryVersion()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "VERSION")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory!.FullName, "VERSION")).Trim();
    }
}
