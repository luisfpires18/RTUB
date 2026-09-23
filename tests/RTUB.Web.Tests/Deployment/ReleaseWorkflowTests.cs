using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Deployment;

/// <summary>
/// Pins the release architecture (docs/release-and-rollback.md) so a later edit cannot quietly
/// undo it: the five RTUB workflows and their responsibilities, build-once, no publish profile,
/// no app-settings calls in public logs, a rollback that can never rebuild, and smoke that always
/// names the build it expects.
///
/// The structural checks read the files as text and run everywhere. The script self-tests need a
/// real bash and run on the Linux CI runner; on Windows they are skipped - run
/// <c>bash scripts/&lt;script&gt;.sh --self-test</c> by hand.
/// </summary>
public class ReleaseWorkflowTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string Workflows = Path.Combine(Root, ".github", "workflows");

    private static string Ci => Read(".github/workflows/ci.yml");
    private static string DeployDev => Read(".github/workflows/deploy-dev.yml");
    private static string DeployProd => Read(".github/workflows/deploy-prod.yml");
    private static string Rollback => Read(".github/workflows/rollback-prod.yml");
    private static string PackageAction => Read(".github/actions/package-release/action.yml");
    private static string DeployAction => Read(".github/actions/deploy-and-verify/action.yml");

    // ---------- the Actions sidebar ----------

    [Fact]
    public void Sidebar_ShowsExactlyTheFiveRtubWorkflows()
    {
        var names = Directory.GetFiles(Workflows, "*.yml")
            .Select(f => File.ReadLines(f).First())
            .Select(line => line.Replace("name:", "").Trim())
            .ToList();

        names.Should().BeEquivalentTo(
            "CI • Build & Test",
            "Deploy • DEV",
            "Deploy • PROD",
            "Rollback • PROD",
            "Database • Refresh DEV from PROD");
    }

    [Fact]
    public void Ci_BuildsAndTestsOnly()
    {
        Triggers(Ci).Should().BeEquivalentTo("pull_request", "workflow_call");
        Ci.Should().NotContainAny("webapps-deploy", "azure/login", "environment:", "id-token", "dotnet publish");
    }

    [Fact]
    public void Ci_RequiresAVersionBumpOnPullRequestsIntoMaster()
    {
        var job = Job(Ci, "version-bump");

        job.Should().Contain("if: github.event_name == 'pull_request' && github.base_ref == 'master'");
        job.Should().Contain("release.sh bump-check");
    }

    [Fact]
    public void DeployDev_RunsOnDevPushes_AfterCi_AndDeploysOnlyRtubDev()
    {
        Triggers(DeployDev).Should().BeEquivalentTo("push");
        DeployDev.Should().MatchRegex(@"push:\s+branches: \[dev\]");
        Job(DeployDev, "ci").Should().Contain("uses: ./.github/workflows/ci.yml");
        Job(DeployDev, "package").Should().Contain("needs: ci");

        var deploy = Job(DeployDev, "deploy");
        deploy.Should().Contain("name: development").And.Contain("app-name: rtub-dev");
        DeployDev.Should().NotMatchRegex(@"app-name: rtub\s*$", "DEV must never name the production app");
        DeployDev.Should().NotContain("rtub.azurewebsites.net");
    }

    [Fact]
    public void DeployProd_RunsOnMasterPushes_InProduction_WithTheSharedConcurrencyGroup()
    {
        Triggers(DeployProd).Should().BeEquivalentTo("push");
        DeployProd.Should().MatchRegex(@"push:\s+branches: \[master\]");
        Job(DeployProd, "ci").Should().Contain("uses: ./.github/workflows/ci.yml");
        Job(DeployProd, "package").Should().Contain("needs: [version, ci]");

        var deploy = Job(DeployProd, "deploy");
        deploy.Should().Contain("name: production").And.Contain("id-token: write");
        deploy.Should().MatchRegex(@"concurrency:\s+group: production\s+cancel-in-progress: false");
        deploy.Should().Contain("app-name: rtub\n");
        DeployProd.Should().NotContain("rtub-dev");
    }

    /// <summary>npm and NuGet code runs in the build; it must never hold a production token.</summary>
    [Fact]
    public void DeployProd_OnlyTheDeployJobCanAuthenticateToAzure()
    {
        foreach (var job in new[] { "version", "package", "tag" })
        {
            Job(DeployProd, job).Should().NotContain("id-token", $"'{job}' must not be able to mint an Azure token");
        }
    }

    /// <summary>
    /// Archive first, then read the zip BACK from the archive and deploy those bytes - the same
    /// fetch Rollback • PROD uses.
    /// </summary>
    [Fact]
    public void DeployProd_DeploysTheZipItReadBackFromTheArchive()
    {
        var deploy = Job(DeployProd, "deploy");

        var put = deploy.IndexOf("release-archive.sh put", StringComparison.Ordinal);
        var get = deploy.IndexOf("release-archive.sh get", StringComparison.Ordinal);
        var ship = deploy.IndexOf("uses: ./.github/actions/deploy-and-verify", StringComparison.Ordinal);

        put.Should().BeGreaterThan(-1);
        get.Should().BeGreaterThan(put);
        ship.Should().BeGreaterThan(get);
        deploy.Should().Contain("package: ${{ runner.temp }}/release/rtub-");
    }

    [Fact]
    public void Rollback_IsManualOnly_TakesAnExplicitVersion_AndSharesTheProductionGroup()
    {
        Triggers(Rollback).Should().BeEquivalentTo("workflow_dispatch");
        Rollback.Should().MatchRegex(@"version:\s+description:[^\n]*\n\s+required: true");
        Rollback.Should().MatchRegex(@"confirm:\s+description:[^\n]*\n\s+required: true");
        Rollback.Should().MatchRegex(@"concurrency:\s+group: production\s+cancel-in-progress: false");
        Rollback.Should().Contain("release-archive.sh get");
    }

    [Fact]
    public void Rollback_NeverBuilds()
    {
        Rollback.Should().NotContainAny(
            "dotnet", "setup-dotnet", "setup-node", "npm", "package-release", "upload-artifact", "release.sh package");
    }

    /// <summary>One implementation of the build, used by both deploy workflows.</summary>
    [Fact]
    public void DotnetPublish_HappensInExactlyOnePlace_ForLinuxX64FrameworkDependent()
    {
        var all = AllWorkflowAndActionText();

        Regex.Matches(all, "dotnet publish").Should().HaveCount(1);
        PackageAction.Should().Contain("dotnet publish").And.Contain("-r linux-x64").And.Contain("--self-contained false");
        PackageAction.Should().Contain("node-version: '22'");
        PackageAction.Should().Contain("release.sh package", "the guards run on the zip itself");
        Regex.Matches(DeployDev + DeployProd, "uses: ./.github/actions/package-release").Should().HaveCount(2);
    }

    [Fact]
    public void NoWorkflow_UsesAPublishProfile()
    {
        AllWorkflowAndActionText().Should().NotContainAny("publish-profile", "AZURE_WEBAPP_PUBLISH_PROFILE");
    }

    /// <summary>
    /// The repository is public, so workflow logs are public. <c>az webapp config appsettings set</c>
    /// echoes every setting, secrets included; production workflows make no app-settings call at all.
    /// </summary>
    [Fact]
    public void ProductionWorkflows_NeverTouchAppSettings()
    {
        (DeployProd + Rollback + PackageAction + DeployAction).Should().NotContain("az webapp config");
    }

    /// <summary>Smoke must prove WHICH build answers, not merely that something healthy does.</summary>
    [Fact]
    public void EveryDeploy_ExpectsAVersionAndACommit()
    {
        DeployAction.Should().Contain("expect-version and expect-commit are both required");
        DeployAction.Should().Contain("EXPECT_VERSION").And.Contain("EXPECT_COMMIT");

        foreach (var workflow in new[] { DeployDev, DeployProd, Rollback })
        {
            workflow.Should().Contain("expect-version:").And.Contain("expect-commit:");
        }
    }

    [Fact]
    public void NewWorkflows_UseTheCurrentActionMajors()
    {
        var text = Ci + DeployDev + DeployProd + Rollback + PackageAction + DeployAction;

        text.Should().NotMatchRegex(@"actions/(checkout|setup-dotnet|setup-node|upload-artifact|download-artifact)@v[1-5]\b");
        text.Should().NotMatchRegex(@"actions/checkout@v6\b|actions/setup-node@v6\b|actions/upload-artifact@v6\b");
        text.Should().NotMatchRegex(@"azure/(login|webapps-deploy)@v[12]\b");
    }

    // ---------- the scripts (bash; Linux CI) ----------

    [Theory]
    [InlineData("release.sh", "release self-test PASSED")]
    [InlineData("release-archive.sh", "release-archive self-test PASSED")]
    [InlineData("smoke-azure.sh", "parser self-test PASSED")]
    public async Task Script_SelfTestPasses(string script, string expected)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(),
            $"Runs on the Linux CI runner. By hand: bash scripts/{script} --self-test");

        var startInfo = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root
        };
        startInfo.ArgumentList.Add(Path.Combine(Root, "scripts", script));
        startInfo.ArgumentList.Add("--self-test");

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);

        process.ExitCode.Should().Be(0, await stdout + await stderr);
        (await stdout).Should().Contain(expected);
    }

    // ---------- helpers ----------

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n");

    private static string AllWorkflowAndActionText() =>
        string.Concat(Directory.GetFiles(Workflows, "*.yml")
            .Concat(Directory.GetFiles(Path.Combine(Root, ".github", "actions"), "action.yml", SearchOption.AllDirectories))
            .Select(f => File.ReadAllText(f)));

    /// <summary>Top-level keys under <c>on:</c>.</summary>
    private static List<string> Triggers(string workflow)
    {
        var on = workflow[(workflow.IndexOf("\non:\n", StringComparison.Ordinal) + 5)..];
        on = on[..on.IndexOf("\n\n", StringComparison.Ordinal)];
        return Regex.Matches(on, @"(?m)^  ([a-z_]+):").Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>The text of one job, from its key to the next job key.</summary>
    private static string Job(string workflow, string id)
    {
        var jobs = workflow[workflow.IndexOf("\njobs:\n", StringComparison.Ordinal)..];
        var start = Regex.Match(jobs, $@"(?m)^  {Regex.Escape(id)}:\n");
        start.Success.Should().BeTrue($"job '{id}' should exist");
        var rest = jobs[(start.Index + start.Length)..];
        var next = Regex.Match(rest, @"(?m)^  [A-Za-z0-9_-]+:\n");
        return next.Success ? rest[..next.Index] : rest;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".github")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
