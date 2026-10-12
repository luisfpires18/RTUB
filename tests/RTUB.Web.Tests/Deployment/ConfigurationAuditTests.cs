using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Deployment;

/// <summary>
/// Task 039: the configuration manifest, the redacted audit scripts and the post-Blazor documentation truths.
///
/// The manifest and documentation checks read files and run everywhere. The script checks run the PowerShell audits
/// on synthetic settings that carry sentinel "secrets" and fail if a sentinel reaches any output file or a template
/// proposes anything but a placeholder for a secret. They need Windows PowerShell or pwsh (both GitHub runners have
/// one) and never touch Azure.
/// </summary>
public class ConfigurationAuditTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string ManifestPath = Path.Combine(Root, "scripts", "config", "required-settings.json");

    // ---------- manifest ----------

    [Fact]
    public void Manifest_IsValidJson_AndEverySettingIsClassified()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var settings = doc.RootElement.GetProperty("settings").EnumerateArray().ToList();
        settings.Should().NotBeEmpty();

        var statuses = new[] { "required", "optional", "absent", "n/a" };
        var names = new HashSet<string>();
        foreach (var s in settings)
        {
            var name = s.GetProperty("name").GetString()!;
            names.Add(name).Should().BeTrue("{0} is listed once", name);
            foreach (var env in new[] { "local", "dev", "prod" })
            {
                statuses.Should().Contain(s.GetProperty(env).GetString(), "{0} has a {1} status", name, env);
            }

            s.GetProperty("secret").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
            s.GetProperty("source").GetArrayLength().Should().BePositive();
            new[] { "match", "differ", "any" }.Should().Contain(s.GetProperty("devProd").GetString());
            s.GetProperty("notes").GetString().Should().NotBeNullOrWhiteSpace();

            if (s.GetProperty("secret").GetBoolean())
            {
                // A secret never has a committed, expected or example value in the manifest.
                s.TryGetProperty("committedDefault", out _).Should().BeFalse(name);
                s.TryGetProperty("expected", out _).Should().BeFalse(name);
                s.TryGetProperty("example", out _).Should().BeFalse(name);
            }
        }
    }

    [Theory]
    [InlineData("WebPush__Enabled")]
    [InlineData("WebPush__VapidSubject")]
    [InlineData("WebPush__VapidPublicKey")]
    [InlineData("WebPush__VapidPrivateKey")]
    [InlineData("Cloudflare__R2__AccountId")]
    [InlineData("Cloudflare__R2__AccessKeyId")]
    [InlineData("Cloudflare__R2__SecretAccessKey")]
    [InlineData("Cloudflare__R2__Bucket")]
    [InlineData("Cloudflare__R2__PublicUrl")]
    [InlineData("Cloudflare__R2__ReferencePublicUrl")]
    [InlineData("Cloudflare__R2__Reference__AccountId")]
    [InlineData("Cloudflare__R2__Reference__AccessKeyId")]
    [InlineData("Cloudflare__R2__Reference__SecretAccessKey")]
    [InlineData("Cloudflare__R2__Reference__Bucket")]
    [InlineData("EmailSettings__SmtpServer")]
    [InlineData("EmailSettings__SmtpPort")]
    [InlineData("EmailSettings__EnableSsl")]
    [InlineData("EmailSettings__SmtpUsername")]
    [InlineData("EmailSettings__SmtpPassword")]
    [InlineData("EmailSettings__SenderEmail")]
    [InlineData("EmailSettings__SenderName")]
    [InlineData("EmailSettings__RecipientEmail")]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    [InlineData("WEBSITE_RUN_FROM_PACKAGE")]
    [InlineData("ConnectionStrings__SqliteConnection")]
    [InlineData("DatabaseBackup__Enabled")]
    [InlineData("Toggles__MapEnabled")]
    [InlineData("Geocoding__DisabledInTests")]
    public void Manifest_ListsTheSetting(string name)
    {
        Settings().Should().ContainKey(name);
    }

    [Fact]
    public void Manifest_EncodesTheEnvironmentRules()
    {
        var s = Settings();

        foreach (var vapid in new[] { "WebPush__VapidSubject", "WebPush__VapidPublicKey", "WebPush__VapidPrivateKey" })
        {
            Status(s[vapid], "dev").Should().Be("required");
            Status(s[vapid], "prod").Should().Be("required");
        }

        s["WebPush__VapidPrivateKey"].GetProperty("secret").GetBoolean().Should().BeTrue();
        s["WebPush__VapidPrivateKey"].GetProperty("devProd").GetString().Should().Be("differ");
        s["Cloudflare__R2__SecretAccessKey"].GetProperty("devProd").GetString().Should().Be("differ");
        Status(s["Cloudflare__R2__Reference__SecretAccessKey"], "prod").Should().Be("absent");
        Expected(s["DatabaseBackup__Enabled"], "dev").Should().Be("false");
        Expected(s["DatabaseBackup__Enabled"], "prod").Should().Be("true");
        Status(s["DatabaseBackup__SecretAccessKey"], "dev").Should().Be("absent");
        Expected(s["ASPNETCORE_ENVIRONMENT"], "dev").Should().Be("Staging");
        Expected(s["ASPNETCORE_ENVIRONMENT"], "prod").Should().Be("Production");
        Expected(s["WEBSITE_RUN_FROM_PACKAGE"], "prod").Should().Be("1");
        Status(s["WEBSITE_RUN_FROM_PACKAGE"], "prod").Should().Be("required");
        Status(s["WEBSITE_RUN_FROM_PACKAGE"], "dev").Should().Be("optional", "DEV intentionally has no run-from-package (040)");
        Expected(s["WEBSITE_RUN_FROM_PACKAGE"], "dev").Should().BeNull();
        s.Keys.Should().NotContain(k => k.StartsWith("IDrive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Manifest_SaysMapsNeedNoKey_AndMessagingTablesArePreserved()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var policies = doc.RootElement.GetProperty("policies");

        policies.GetProperty("mapsRequireExternalKey").GetBoolean().Should().BeFalse();
        policies.GetProperty("maps").GetString().Should().Contain("No API key");
        Settings().Values.Where(s => s.GetProperty("group").GetString() == "maps")
            .Should().OnlyContain(s => !s.GetProperty("secret").GetBoolean(), "maps need no secret");

        policies.GetProperty("messagingTablesPreserved").GetBoolean().Should().BeTrue();
        policies.GetProperty("messagingTables").EnumerateArray().Select(t => t.GetString())
            .Should().BeEquivalentTo("Conversations", "Messages", "MessageReactions", "ConversationUserSettings");
    }

    [Fact]
    public void LocalAuditOutput_IsGitIgnored_AndNothingUnderItIsTracked()
    {
        File.ReadAllLines(Path.Combine(Root, ".gitignore")).Select(l => l.Trim()).Should().Contain(".local/");

        var git = Run("git", ["check-ignore", "-q", ".local/config-audit/dev-app-settings.audit.json"]);
        if (git.Started)
        {
            git.ExitCode.Should().Be(0, "git must ignore .local/config-audit/");
            var tracked = Run("git", ["ls-files", "--", ".local"]);
            tracked.Output.Trim().Should().BeEmpty("generated audit outputs are never committed");
        }
    }

    [Fact]
    public void Runbook_DocumentsGeneratedOutputs_AndTheRunFromPackagePolicy()
    {
        var runbook = Regex.Replace(File.ReadAllText(Path.Combine(Root, "docs", "configuration-runbook.md")), @"\s+", " ");

        runbook.Should().Contain("`.local/config-audit/` — **generated, git-ignored").And.Contain("never committed, safe to delete")
            .And.Contain("rerun the audit scripts to regenerate it");
        runbook.Should().Contain("PROD keeps `=1`; DEV intentionally does not have it")
            .And.Contain("Do not re-add it to `rtub-dev`").And.Contain("informational noise");
        runbook.Should().Contain("'WEBSITE_RUN_FROM_PACKAGE=1'", "the PROD baseline keeps run-from-package");
        runbook.Should().Contain("Maps need no API key").And.Contain("OpenStreetMap");
        runbook.Should().NotContain("CARTO basemap tiles");
    }

    // ---------- the scripts (PowerShell, synthetic data only) ----------

    private const string DevSentinel = "SENTINEL-DEV-SECRET-7f3a9c";
    private const string SharedSentinel = "SENTINEL-SHARED-R2-SECRET-55e1";
    private const string ProdSentinel = "SENTINEL-PROD-VAPID-PRIVATE-0b2d";
    private const string LocalSentinel = "SENTINEL-LOCAL-PASSWORD-91cc";

    [Fact]
    public void AzureAudit_RedactsValues_WarnsOnTheRules_AndTemplatesUsePlaceholdersOnly()
    {
        var shell = PowerShellOrSkip();
        var temp = Directory.CreateTempSubdirectory("rtub-config-audit-");
        try
        {
            var dev = Path.Combine(temp.FullName, "dev.json");
            var prod = Path.Combine(temp.FullName, "prod.json");
            File.WriteAllText(dev, JsonSerializer.Serialize(new[]
            {
                Setting("ASPNETCORE_ENVIRONMENT", "Staging"),
                Setting("ConnectionStrings__SqliteConnection", "Data Source=/home/site/data/dev.db"),
                Setting("AdminUser__Password", DevSentinel),
                Setting("Cloudflare__R2__SecretAccessKey", SharedSentinel),
                Setting("DatabaseBackup__Enabled", "true"),
                Setting("IDrive__SecretKey", DevSentinel + "-idrive"),
            }));
            File.WriteAllText(prod, JsonSerializer.Serialize(new[]
            {
                Setting("ASPNETCORE_ENVIRONMENT", "Production"),
                Setting("WEBSITE_RUN_FROM_PACKAGE", "0"),
                Setting("ConnectionStrings__SqliteConnection", "Data Source=/tmp/app.db"),
                Setting("Cloudflare__R2__SecretAccessKey", SharedSentinel),
                Setting("WebPush__VapidPrivateKey", ProdSentinel),
                Setting("Messaging__DropTablesOnStartup", "true"),
            }));

            var outDir = Path.Combine(temp.FullName, "out");
            var result = RunScript(shell, "audit-azure-app-settings.ps1",
                "-DevSettingsFile", dev, "-ProdSettingsFile", prod, "-OutputDirectory", outDir);
            result.ExitCode.Should().Be(0, result.Output);
            result.Output.Should().NotContainAny(DevSentinel, SharedSentinel, ProdSentinel);

            var files = new[] { "dev-app-settings.audit.md", "dev-app-settings.audit.json", "prod-app-settings.audit.md",
                "prod-app-settings.audit.json", "set-dev-app-settings.template.ps1", "set-prod-app-settings.template.ps1" };
            foreach (var file in files)
            {
                File.Exists(Path.Combine(outDir, file)).Should().BeTrue(file);
                File.ReadAllText(Path.Combine(outDir, file)).Should().NotContainAny(new[] { DevSentinel, SharedSentinel, ProdSentinel }, file);
            }

            var devReport = File.ReadAllText(Path.Combine(outDir, "dev-app-settings.audit.md"));
            var prodReport = File.ReadAllText(Path.Combine(outDir, "prod-app-settings.audit.md"));
            devReport.Should().NotContain("WEBSITE_RUN_FROM_PACKAGE: ", "DEV intentionally runs without it (040)");
            devReport.Should().Contain("WebPush VAPID settings incomplete").And.Contain("Cloudflare__R2__SecretAccessKey: shares its value with PROD (must differ)")
                .And.Contain("DatabaseBackup__Enabled: not the expected value 'false'").And.Contain("IDrive__SecretKey: deprecated");
            prodReport.Should().Contain("WEBSITE_RUN_FROM_PACKAGE: not the expected value '1'")
                .And.Contain("does not start with 'Data Source=/home/site/data/'")
                .And.Contain("EmailSettings incomplete").And.Contain("Messaging__DropTablesOnStartup: deprecated");

            using (var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "dev-app-settings.audit.json"))))
            {
                var row = json.RootElement.GetProperty("settings").EnumerateArray()
                    .Single(r => r.GetProperty("name").GetString() == "AdminUser__Password");
                row.GetProperty("length").GetInt32().Should().Be(DevSentinel.Length);
                row.GetProperty("fingerprint").GetString().Should().MatchRegex("^sha256:[0-9a-f]{12}$");
                row.TryGetProperty("value", out _).Should().BeFalse("no value is ever recorded");
            }

            foreach (var template in new[] { "set-dev-app-settings.template.ps1", "set-prod-app-settings.template.ps1" })
            {
                var text = File.ReadAllText(Path.Combine(outDir, template));
                text.Should().Contain("--settings @settings --output none").And.Contain("<[^>]+>", "the placeholder guard is present");
                foreach (var secret in SecretNames())
                {
                    foreach (Match m in Regex.Matches(text, $"'{Regex.Escape(secret)}=([^']*)'"))
                    {
                        m.Groups[1].Value.Should().MatchRegex("^<(DEV|PROD)_[A-Z0-9_]+>$", "{0} in {1} is a placeholder", secret, template);
                    }
                }
            }

            File.ReadAllText(Path.Combine(outDir, "set-dev-app-settings.template.ps1"))
                .Should().Contain("'WebPush__VapidPrivateKey=<DEV_VAPID_PRIVATE_KEY>'").And.Contain("'DatabaseBackup__Enabled=false'")
                .And.Contain("az webapp config appsettings set -g rtub_group -n rtub-dev");
            File.ReadAllText(Path.Combine(outDir, "set-prod-app-settings.template.ps1"))
                .Should().Contain("'ASPNETCORE_ENVIRONMENT=Production'").And.Contain("'EmailSettings__SmtpPassword=<PROD_SMTP_PASSWORD>'")
                .And.Contain("az webapp config appsettings set -g rtub_group -n rtub ");
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void AzureAudit_RefusesIncludeValues_AndTrackedOutputPaths()
    {
        var shell = PowerShellOrSkip();
        var temp = Directory.CreateTempSubdirectory("rtub-config-audit-");
        try
        {
            var empty = Path.Combine(temp.FullName, "empty.json");
            File.WriteAllText(empty, "[]");

            var values = RunScript(shell, "audit-azure-app-settings.ps1", "-IncludeValues",
                "-DevSettingsFile", empty, "-ProdSettingsFile", empty, "-OutputDirectory", Path.Combine(temp.FullName, "out"));
            values.ExitCode.Should().NotBe(0);
            values.Output.Should().Contain("IncludeValues is deliberately not implemented");

            var tracked = RunScript(shell, "audit-azure-app-settings.ps1",
                "-DevSettingsFile", empty, "-ProdSettingsFile", empty, "-OutputDirectory", "docs/config-audit-test");
            tracked.ExitCode.Should().NotBe(0);
            tracked.Output.Should().Contain("must be under .local/");
            Directory.Exists(Path.Combine(Root, "docs", "config-audit-test")).Should().BeFalse();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void LocalAudit_RedactsValues_AndTemplateUsesPlaceholdersOnly()
    {
        var shell = PowerShellOrSkip();
        var temp = Directory.CreateTempSubdirectory("rtub-config-audit-");
        try
        {
            var list = Path.Combine(temp.FullName, "user-secrets.txt");
            File.WriteAllLines(list, new[]
            {
                $"Cloudflare:R2:SecretAccessKey = {LocalSentinel}",
                $"IDrive:AccessKey = {LocalSentinel}-idrive",
            });

            var outDir = Path.Combine(temp.FullName, "out");
            var result = RunScript(shell, "audit-local-user-secrets.ps1", "-UserSecretsListFile", list, "-OutputDirectory", outDir);
            result.ExitCode.Should().Be(0, result.Output);
            result.Output.Should().NotContain(LocalSentinel);

            foreach (var file in new[] { "local-user-secrets.audit.md", "local-user-secrets.audit.json", "set-local-user-secrets.template.ps1" })
            {
                File.ReadAllText(Path.Combine(outDir, file)).Should().NotContain(LocalSentinel, file);
            }

            var report = File.ReadAllText(Path.Combine(outDir, "local-user-secrets.audit.md"));
            report.Should().Contain("Missing required local setting AdminUser__Password").And.Contain("IDrive__AccessKey");
            report.Should().MatchRegex(@"`Cloudflare__R2__SecretAccessKey` \| optional \| ok \| user-secrets \| True \| " + LocalSentinel.Length + @" \| sha256:[0-9a-f]{12}");

            var template = File.ReadAllText(Path.Combine(outDir, "set-local-user-secrets.template.ps1"));
            template.Should().Contain("Write-Error 'Template not edited")
                .And.Contain("dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj \"AdminUser:Password\" \"<LOCAL_ADMIN_PASSWORD>\"");
            foreach (var secret in SecretNames())
            {
                foreach (Match m in Regex.Matches(template, $"\"{Regex.Escape(secret.Replace("__", ":"))}\" \"([^\"]*)\""))
                {
                    m.Groups[1].Value.Should().MatchRegex("^<LOCAL_[A-Z0-9_]+>$", secret);
                }
            }
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void VapidGenerator_PrintsAValidP256PairInBase64Url_AndWritesNothing()
    {
        var shell = PowerShellOrSkip();
        var before = Directory.GetFiles(Path.Combine(Root, "scripts", "config"));

        var result = RunScript(shell, "generate-vapid-keys.ps1");

        result.ExitCode.Should().Be(0, result.Output);
        result.Output.Should().Contain("do not commit").And.Contain("invalidates every").And.Contain("WebPush__VapidSubject=mailto:<contact-address>");
        var publicKey = Regex.Match(result.Output, @"WebPush__VapidPublicKey=([A-Za-z0-9_-]+)").Groups[1].Value;
        var privateKey = Regex.Match(result.Output, @"WebPush__VapidPrivateKey=([A-Za-z0-9_-]+)").Groups[1].Value;
        var pub = FromBase64Url(publicKey);
        pub.Should().HaveCount(65);
        pub[0].Should().Be(4, "uncompressed P-256 point");
        FromBase64Url(privateKey).Should().HaveCount(32);
        Directory.GetFiles(Path.Combine(Root, "scripts", "config")).Should().BeEquivalentTo(before);
    }

    // ---------- documentation truths ----------

    [Fact]
    public void Docs_DoNotSayTheMessagingTablesShouldBeDropped()
    {
        var negation = new Regex(@"(?i)\b(never|not|no|cancelled|preserved|preserve)\b");
        var mention = new Regex(@"(?i)(conversation|messag)");
        var drop = new Regex(@"(?i)\bdrop");

        foreach (var (file, lines) in DocLines())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (!drop.IsMatch(lines[i]) || !mention.IsMatch(lines[i])) continue;
                var window = string.Join(' ', lines.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3));
                negation.IsMatch(window).Should().BeTrue("{0}:{1} must not plan a messaging drop: {2}", file, i + 1, lines[i]);
            }
        }
    }

    [Fact]
    public void Docs_DoNotSayBlazorIsActive()
    {
        var active = new Regex(@"(?i)(Blazor Web App|Blazor Web Application|Blazor Interactive Server app|built with \**Blazor|Blazor Server host|src/RTUB\.Shared/Components|bUnit \(component tests)");
        foreach (var (file, lines) in DocLines())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                active.IsMatch(lines[i]).Should().BeFalse("{0}:{1} describes Blazor as active: {2}", file, i + 1, lines[i]);
            }
        }

        var stack = File.ReadAllText(Path.Combine(Root, "README.md")) + File.ReadAllText(Path.Combine(Root, ".github", "copilot-instructions.md"));
        stack.Should().Contain("React 19").And.Contain("no Blazor", "the stack docs say the frontend is React-only");
    }

    [Fact]
    public void Docs_DoNotPresentIDriveAsTheCurrentStorage()
    {
        var historical = new Regex(@"(?i)(historical|dead|stale|leftover|delete|removed?|absent|deprecated|nothing reads|no code)");
        foreach (var (file, lines) in DocLines())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("IDrive", StringComparison.OrdinalIgnoreCase)) continue;
                var window = string.Join(' ', lines.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3));
                historical.IsMatch(window).Should().BeTrue("{0}:{1} mentions IDrive as if current: {2}", file, i + 1, lines[i]);
            }
        }

        File.ReadAllText(Path.Combine(Root, "README.md")).Should().Contain("Cloudflare R2");
    }

    [Fact]
    public void ConfigDocsAndScripts_CarryNoRealLookingSecret()
    {
        var files = new[]
        {
            Path.Combine(Root, "docs", "configuration-runbook.md"),
            Path.Combine(Root, "README.md"),
            Path.Combine(Root, "docs", "ci-cd-and-azure-environments.md"),
            ManifestPath,
        }.Concat(Directory.GetFiles(Path.Combine(Root, "scripts", "config"), "*.ps1"));

        // A long token with letters and several digits (keys, tokens), not a setting name or a hyphenated file name.
        var token = new Regex(@"(?<![A-Za-z0-9_\-])(?=(?:[A-Za-z_\-]*\d){4})(?=[A-Za-z0-9_\-]*[A-Za-z])[A-Za-z0-9_\-]{32,}(?![A-Za-z0-9_\-])");
        var assignment = new Regex(@"(?i)(password|secret|accesskey|privatekey)[A-Za-z_:]*[""']?\s*[=:]\s*[""']?(?<value>[^\s""'<`,)]{8,})");

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("-----BEGIN", file);
            token.Matches(text).Select(m => m.Value).Where(v => !v.Contains("__") && !Regex.IsMatch(v, "^[a-z0-9]+(-[a-z0-9]+)+$"))
                .Should().BeEmpty("{0} must hold placeholders only", file);
            if (file.EndsWith(".md") || file.EndsWith(".json"))
            {
                assignment.Matches(text).Select(m => m.Groups["value"].Value)
                    .Where(v => !v.StartsWith('<') && !v.StartsWith('$') && !v.StartsWith('{'))
                    .Should().BeEmpty("{0} must not assign a secret a literal value", file);
            }
        }
    }

    // ---------- helpers ----------

    private static Dictionary<string, JsonElement> Settings()
    {
        var doc = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        return doc.RootElement.GetProperty("settings").EnumerateArray()
            .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.Clone());
    }

    private static IEnumerable<string> SecretNames() =>
        Settings().Values.Where(s => s.GetProperty("secret").GetBoolean()).Select(s => s.GetProperty("name").GetString()!);

    private static string? Status(JsonElement setting, string env) => setting.GetProperty(env).GetString();

    private static string? Expected(JsonElement setting, string env) =>
        setting.TryGetProperty("expected", out var e) && e.TryGetProperty(env, out var v) ? v.GetString() : null;

    private static object Setting(string name, string value) => new { name, value, slotSetting = false };

    /// <summary>Every maintained Markdown doc: root docs, docs/ (ADRs excluded: append-only history), agents, RTUB skills.</summary>
    private static IEnumerable<(string File, string[] Lines)> DocLines()
    {
        var files = new List<string> { "README.md", "CLAUDE.md", "STATE.md" }.Select(f => Path.Combine(Root, f))
            .Concat(Directory.GetFiles(Path.Combine(Root, "docs"), "*.md", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.Combine("architecture", "adr"))))
            .Concat(Directory.GetFiles(Path.Combine(Root, ".github"), "*.md", SearchOption.AllDirectories))
            .Concat(Directory.GetDirectories(Path.Combine(Root, ".claude", "skills"), "rtub-*")
                .SelectMany(d => Directory.GetFiles(d, "*.md", SearchOption.AllDirectories)));
        foreach (var file in files.Where(File.Exists))
        {
            yield return (Path.GetRelativePath(Root, file), File.ReadAllLines(file));
        }
    }

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    private static string PowerShellOrSkip()
    {
        foreach (var candidate in new[] { "pwsh", "powershell" })
        {
            var probe = Run(candidate, ["-NoProfile", "-Command", "$PSVersionTable.PSVersion.Major"]);
            if (probe.Started && probe.ExitCode == 0)
            {
                return candidate;
            }
        }

        Assert.Skip("Needs pwsh or Windows PowerShell; by hand: powershell -File scripts/config/audit-azure-app-settings.ps1");
        return null!;
    }

    private static (bool Started, int ExitCode, string Output) RunScript(string shell, string script, params string[] arguments)
    {
        var args = new List<string> { "-NoProfile", "-NonInteractive" };
        if (OperatingSystem.IsWindows()) args.AddRange(["-ExecutionPolicy", "Bypass"]);
        args.Add("-File");
        args.Add(Path.Combine(Root, "scripts", "config", script));
        args.AddRange(arguments);
        return Run(shell, args.ToArray());
    }

    private static (bool Started, int ExitCode, string Output) Run(string fileName, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        Process? process;
        try { process = Process.Start(startInfo); }
        catch (System.ComponentModel.Win32Exception) { return (false, -1, string.Empty); }
        if (process == null) return (false, -1, string.Empty);

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"{fileName} did not finish");
            }

            // Whitespace collapsed: Windows PowerShell wraps error text at the console width.
            return (true, process.ExitCode, Regex.Replace(stdout.Result + stderr.Result, @"\s+", " "));
        }
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
