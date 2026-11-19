using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.Deployment;

/// <summary>
/// Task 039: the configuration manifest, the redacted audit scripts and the post-Blazor documentation truths.
///
/// The manifest and documentation checks read files and run everywhere. The script checks run the PowerShell audits
/// on synthetic settings that carry sentinel "secrets" and fail if a sentinel reaches any output file or a template
/// proposes anything but a placeholder for a secret. They need Windows PowerShell or pwsh (both GitHub runners have
/// one) and never touch Azure.
/// </summary>
public partial class ConfigurationAuditTests
{
    private static readonly string Root = RepoPaths.Root();
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

    /// <summary>#226 F17: settings no code reads are gone from the manifest and flagged for deletion if they reappear.</summary>
    [Theory]
    [InlineData("Toggles__MapEnabled")]
    [InlineData("LoginPopup__Enabled")]
    [InlineData("LoginPopup__Description")]
    [InlineData("UseSqlServer")]
    [InlineData("ImageCaching__CacheDurationInSeconds")]
    [InlineData("Gallery__MaxVideoSizeMB")]
    public void Manifest_FlagsRemovedSettings_AsDeprecated(string name)
    {
        Settings().Should().NotContainKey(name);
        using var doc = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        doc.RootElement.GetProperty("deprecatedPatterns").EnumerateArray()
            .Select(d => d.GetProperty("pattern").GetString()!)
            .Should().Contain(pattern => Regex.IsMatch(name, pattern), name);
        Settings().Keys.Should().Contain("GalleryMedia__MaxVideoSize").And.NotContain("Gallery__MaxVideoSizeMB");
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
}
