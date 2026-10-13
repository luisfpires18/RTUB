using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 036 (docs/react-identity.md): /forgot-password, /reset-password and /confirm-email are React pages over
/// /api/account/*; /Error is a fixed server page on purpose; /share and the PWA share target are gone; no Blazor page is
/// left. The flows themselves are covered by AccountRecoveryApiTests.
/// </summary>
public class IdentityAndShareSourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Web = Path.Combine(Root, "src", "RTUB.Web");
    private static readonly string Portal = Path.Combine(Web, "portal", "src");

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(path)).Replace("\r\n", "\n");

    [Fact]
    public void NoBlazorPage_IsLeft()
    {
        Directory.GetFiles(Web, "*.razor", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"^@page\s", RegexOptions.Multiline))
            .Select(f => Path.GetRelativePath(Web, f))
            .Should().BeEmpty("every page is React or a server endpoint since task 036");
    }

    [Fact]
    public void TheIdentityPages_AreReact_AndErrorAndShareAreServerOwned()
    {
        var main = Read(Portal, "main.tsx");
        var program = Read(Web, "Program.cs");

        foreach (var (route, page) in new[] { ("/forgot-password", "ForgotPassword"), ("/reset-password", "ResetPassword"), ("/confirm-email", "ConfirmEmail") })
        {
            main.Should().Contain($"'{route}': lazy(() => import('./{page}')),");
        }

        program.Should().Contain("new[] { \"/forgot-password\", \"/reset-password\", \"/confirm-email\" }")
            .And.Contain("app.MapAccountRecoveryEndpoints();")
            .And.Contain("app.Map(\"/Error\",")
            .And.Contain("app.UseExceptionHandler(\"/Error\");")
            .And.Contain("app.MapMethods(\"/share\", [\"GET\", \"HEAD\", \"POST\"], () => Results.NotFound());");
        main.Should().NotContain("'/Error'").And.NotContain("'/share'");
        File.Exists(Path.Combine(Web, "wwwroot", "error.html")).Should().BeTrue();
        Read(Web, "wwwroot", "error.html").Should().NotContain("<script").And.NotContain("style=");
    }

    [Fact]
    public void EveryAccountCall_NeedsTheAntiforgeryToken_AndIsNeverCached()
    {
        var endpoints = Read(Web, "Endpoints", "AccountRecoveryEndpoints.cs");

        endpoints.Should().Contain(".AddEndpointFilter(EventEndpoints.NoStore)").And.Contain(".AddEndpointFilter(EventEndpoints.RequireAntiforgery);");
        Regex.Matches(endpoints, @"account\.Map(Get|Put|Delete)\(").Should().BeEmpty("only POSTs: tokens travel in the body");
    }

    [Fact]
    public void TheTokenPages_KeepTokensOutOfStorageTheAddressBarAndTheLogs()
    {
        foreach (var file in new[] { "ForgotPassword.tsx", "ResetPassword.tsx", "ConfirmEmail.tsx", "accountApi.ts" })
        {
            var source = Read(Portal, file);
            source.Should().NotContain("localStorage").And.NotContain("sessionStorage").And.NotContain("console.");
        }

        Read(Portal, "accountApi.ts").Should().Contain("history.replaceState(null, '', location.pathname)");
        Read(Portal, "ResetPassword.tsx").Should().Contain("let link = takeLinkQuery('code', 'email');")
            .And.Contain("link = { code: null, email: null };", "forgotten once used")
            .And.NotContain("useState(link", "never in React state");
        Read(Portal, "ConfirmEmail.tsx").Should().Contain("const link = takeLinkQuery('userId', 'code');");

        var service = Read(Root, "src", "RTUB.Application", "Services", "AccountRecoveryService.cs");
        Regex.Matches(service, @"_logger\.Log\w+\([^;]*\b(token|callbackUrl|Code)\b", RegexOptions.Singleline).Should().BeEmpty("no token is logged");
    }

    [Fact]
    public void TheShareTarget_IsGone()
    {
        using var manifest = JsonDocument.Parse(Read(Web, "wwwroot", "manifest.webmanifest"));
        manifest.RootElement.TryGetProperty("share_target", out _).Should().BeFalse();
        Read(Web, "wwwroot", "service-worker.js").Should().NotContain("share");
        File.Exists(Path.Combine(Web, "Pages", "Share.razor")).Should().BeFalse();
        foreach (var file in Directory.GetFiles(Portal))
        {
            File.ReadAllText(file).Should().NotContain("'/share'").And.NotContain("\"/share\"");
        }
    }

    [Fact]
    public void TheNewPages_SayNothingAboutTheMove_AndLinkNoRemovedModule()
    {
        foreach (var file in new[] { "ForgotPassword.tsx", "ResetPassword.tsx", "ConfirmEmail.tsx", "accountApi.ts" }.Select(f => Path.Combine(Portal, f))
                     .Append(Path.Combine(Web, "wwwroot", "error.html")))
        {
            var text = File.ReadAllText(file);
            var visible = Regex.Replace(text, @"/\*\*.*?\*/|//[^\n]*|<!--.*?-->", string.Empty, RegexOptions.Singleline);
            visible.Should().NotContainEquivalentOf("migra").And.NotContainEquivalentOf("blazor");
            foreach (var removed in new[] { "/games", "/bets", "/my-tuno", "/messages", "'/images'", "/labels" })
            {
                text.Should().NotContain(removed);
            }
        }

        Read(Portal, "content.ts").Should().NotContain("legacy", "no Blazor page is left to link out to");
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
