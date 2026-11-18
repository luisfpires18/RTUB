// Release bash scripts self-test
using System.Diagnostics;
using FluentAssertions;
using RTUB.Application.Data;

namespace RTUB.Web.Tests.Deployment;

public partial class ReleaseWorkflowTests
{
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

    /// <summary>
    /// Deploy • PROD refuses a release that does not know a migration of the live one. The squash removed 180 ids,
    /// so the check must hold the same two ids as the app, or the next release is refused (or a real gap is let through).
    /// </summary>
    [Fact]
    public void SchemaGate_KnowsTheSquash_WithTheSameIdsAsTheApp()
    {
        var script = Read("scripts/release.sh");

        script.Should().Contain($"BASELINE = \"{MigrationBaseline.BaselineId}\"");
        script.Should().Contain($"SQUASHED_THROUGH = \"{MigrationBaseline.LastSquashedId}\"");
    }
}
