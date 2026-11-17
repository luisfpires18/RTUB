// Configuration audit PowerShell scripts run on synthetic sentinel settings
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Deployment;

public partial class ConfigurationAuditTests
{
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
            Regex.IsMatch(tracked.Output, @"must be.*under \.local/", RegexOptions.Singleline).Should().BeTrue();
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
}
