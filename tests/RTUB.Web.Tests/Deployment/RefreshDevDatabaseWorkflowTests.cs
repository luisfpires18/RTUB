using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Deployment;

/// <summary>
/// Pins how <c>refresh-dev-database.yml</c> decides which Azure DEV database file it replaces.
///
/// The first live refresh found the workflow hardcoding <c>site/data/rtub-dev.db</c> while
/// rtub-dev's <c>ConnectionStrings__SqliteConnection</c> had moved to another file, so it backed
/// up, replaced and cleaned the sidecars of an empty 4 KB database the app never opened. The file
/// is now resolved from rtub-dev's own setting by <c>scripts/resolve-dev-db-path.sh</c>. These
/// tests fail if a literal file name, or an <c>env:</c> default that would shadow the resolved
/// value, ever comes back.
///
/// The structural checks read the workflow as text and run everywhere. The three that execute
/// the resolver need bash and run on the Linux CI runner; on Windows they are skipped, and
/// <c>bash scripts/resolve-dev-db-path.sh --self-test</c> is the by-hand equivalent.
/// </summary>
public class RefreshDevDatabaseWorkflowTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string WorkflowPath = Path.Combine(Root, ".github", "workflows", "refresh-dev-database.yml");
    private static readonly string ResolverPath = Path.Combine(Root, "scripts", "resolve-dev-db-path.sh");

    private const string LoginStep = "Azure login (OIDC federated credential)";
    private const string ResolveStep = "Resolve the DEV database path from rtub-dev's configuration";
    private const string StopStep = "Stop rtub-dev";
    private const string ReplaceStep = "Replace the DEV database";
    private const string RollbackStep = "Roll the DEV database back";

    // ---------- the workflow resolves the file; it never names one ----------

    [Fact]
    public void Workflow_ResolvesTheDatabaseFileInsteadOfHardcodingIt()
    {
        var code = WorkflowCode();

        // No literal DEV database file, in any form.
        code.Should().NotMatchRegex(@"site/data/", "the file must come from rtub-dev's own setting");
        code.Should().NotMatchRegex(@"rtub-dev[A-Za-z0-9._-]*\.db", "no DEV database file name may be hardcoded");

        // No env: default at workflow, job or step level. A default is how the stale file name
        // got in, and one would shadow or race the resolved value.
        code.Should().NotMatchRegex(@"(?m)^\s*DEV_DB_PATH\s*:");

        // Exported exactly once, into $GITHUB_ENV, from the resolver's output.
        var exports = Regex.Matches(code, @"DEV_DB_PATH=.*");
        exports.Should().ContainSingle();
        exports[0].Value.Should().Contain("$dev_db_path").And.Contain("$GITHUB_ENV");

        // Read from the one setting the app reads, selected server-side, so the full settings
        // list - which carries secrets - is never pulled into the log.
        var settingsRead = Regex.Match(JoinContinuations(code), @"az webapp config appsettings list[^\n]*");
        settingsRead.Success.Should().BeTrue();
        settingsRead.Value.Should().Contain("--query").And.Contain("ConnectionStrings__SqliteConnection");
    }

    /// <summary>
    /// Resolving comes after the login it needs and before anything is stopped, so a missing or
    /// unexpected setting fails the run with rtub-dev still up and untouched.
    /// </summary>
    [Fact]
    public void ResolveStep_RunsAfterLoginAndBeforeAnythingIsStopped()
    {
        var order = StepNames();

        order.IndexOf(LoginStep).Should().BeGreaterThanOrEqualTo(0);
        order.IndexOf(ResolveStep).Should().BeGreaterThan(order.IndexOf(LoginStep));
        order.IndexOf(StopStep).Should().BeGreaterThan(order.IndexOf(ResolveStep));
        order.IndexOf(ReplaceStep).Should().BeGreaterThan(order.IndexOf(StopStep));
    }

    /// <summary>
    /// Unset or empty, every VFS call in these steps would target the <c>/home</c> root. Both
    /// destructive steps refuse outright before acquiring a token.
    /// </summary>
    [Theory]
    [InlineData(ReplaceStep)]
    [InlineData(RollbackStep)]
    public void DestructiveSteps_RefuseAnUnresolvedPath(string step)
    {
        var body = StepBlock(step);
        var guard = body.IndexOf("${DEV_DB_PATH:?", StringComparison.Ordinal);

        guard.Should().BeGreaterThan(-1, $"'{step}' must refuse an unset or empty DEV_DB_PATH");
        guard.Should().BeLessThan(body.IndexOf("get-access-token", StringComparison.Ordinal),
            "the guard must run before any Azure or Kudu call");
    }

    /// <summary>Unchanged by the fix, pinned because the fix edited this file.</summary>
    [Fact]
    public void Workflow_IsManualOnly()
    {
        var code = WorkflowCode();
        var on = code[code.IndexOf("\non:", StringComparison.Ordinal)..code.IndexOf("\njobs:", StringComparison.Ordinal)];

        on.Should().Contain("workflow_dispatch");
        on.Should().NotContainAny("push", "pull_request", "schedule", "workflow_run", "repository_dispatch");
    }

    // ---------- the resolver itself (bash; Linux CI) ----------

    [Fact]
    public async Task Resolver_SelfTestPasses()
    {
        SkipUnlessBashIsTheRealOne();

        var (exitCode, stdout, stderr) = await RunResolverAsync(connectionString: null, "--self-test");

        exitCode.Should().Be(0, stderr);
        stdout.Should().Contain("self-test PASSED");
    }

    [Fact]
    public async Task Resolver_PrintsOnlyTheValidatedVfsPath()
    {
        SkipUnlessBashIsTheRealOne();

        var (exitCode, stdout, stderr) = await RunResolverAsync("Data Source=/home/site/data/rtub-dev-v3.db");

        exitCode.Should().Be(0, stderr);
        stdout.Should().Be("site/data/rtub-dev-v3.db\n");
        stderr.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolver_RejectionNeverEchoesTheSetting()
    {
        SkipUnlessBashIsTheRealOne();

        var (exitCode, stdout, stderr) =
            await RunResolverAsync("Data Source=/home/site/data/rtub-dev-v3.db;Password=hunter2-secret");

        exitCode.Should().Be(1);
        stdout.Should().BeEmpty("nothing may reach $GITHUB_ENV on a rejection");
        (stdout + stderr).Should().NotContain("hunter2").And.NotContain("rtub-dev-v3");
    }

    // ---------- helpers ----------

    /// <summary>
    /// On Windows, <c>bash</c> can resolve to the WSL launcher in System32 ahead of Git Bash, so
    /// the result would say nothing reliable about the script. CI runs these on Linux.
    /// </summary>
    private static void SkipUnlessBashIsTheRealOne() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(),
            "Runs on the Linux CI runner. By hand: bash scripts/resolve-dev-db-path.sh --self-test");

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunResolverAsync(
        string? connectionString, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root
        };

        startInfo.ArgumentList.Add(ResolverPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Passed the way the workflow passes it: through the environment, never argv.
        startInfo.Environment.Remove("SQLITE_CONNECTION_STRING");
        if (connectionString != null)
        {
            startInfo.Environment["SQLITE_CONNECTION_STRING"] = connectionString;
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);

        return (process.ExitCode, await stdout, await stderr);
    }

    /// <summary>The workflow minus full-line comments, which are allowed to tell the history.</summary>
    private static string WorkflowCode() =>
        string.Join('\n', File.ReadAllLines(WorkflowPath).Where(line => !line.TrimStart().StartsWith('#')));

    /// <summary>Joins shell line continuations so one command reads as one line.</summary>
    private static string JoinContinuations(string text) => Regex.Replace(text, @"\\\n\s*", " ");

    private static readonly Regex StepHeader = new(@"(?m)^\s*- name: (?<name>.+?)\s*$");

    private static List<string> StepNames() =>
        StepHeader.Matches(WorkflowCode()).Select(m => m.Groups["name"].Value).ToList();

    private static string StepBlock(string name)
    {
        var code = WorkflowCode();
        var headers = StepHeader.Matches(code).ToList();
        var index = headers.FindIndex(m => m.Groups["name"].Value == name);

        index.Should().BeGreaterThanOrEqualTo(0, $"step '{name}' must exist");

        var start = headers[index].Index;
        var end = index + 1 < headers.Count ? headers[index + 1].Index : code.Length;
        return code[start..end];
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
