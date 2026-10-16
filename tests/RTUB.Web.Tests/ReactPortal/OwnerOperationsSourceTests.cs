using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 035 (docs/react-owner-operations.md): /emails, /notifications, /users, /owner/tracing and /owner/db are React
/// pages over an Owner-only /api/owner; their Blazor pages and page-only components are gone, and the Blazor navbar
/// shows them to the Owner only, as full navigations. Who gets in is covered by OwnerOperationsApiTests.
/// </summary>
public class OwnerOperationsSourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Web = Path.Combine(Root, "src", "RTUB.Web");
    private static readonly string Portal = Path.Combine(Web, "portal", "src");

    private static readonly string[] Routes = ["/emails", "/notifications", "/users", "/owner/tracing", "/owner/db"];

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(path)).Replace("\r\n", "\n");

    [Theory]
    [InlineData("src/RTUB.Web/Pages/Operations/Emails.razor")]
    [InlineData("src/RTUB.Web/Pages/Operations/Notifications.razor")]
    [InlineData("src/RTUB.Web/Pages/Operations/AuditLog.razor")]
    [InlineData("src/RTUB.Web/Pages/Operations/DatabaseViewer.razor")]
    [InlineData("src/RTUB.Web/Pages/Management/UserRoles.razor")]
    [InlineData("src/RTUB.Shared/Components/Email/EmailRecipientsPreview.razor")]
    [InlineData("src/RTUB.Shared/Components/Cards/UserCard.razor")]
    [InlineData("src/RTUB.Shared/Components/Cards/TracingCard.razor")]
    [InlineData("src/RTUB.Shared/Components/UI/LoginStatisticsButton.razor")]
    [InlineData("src/RTUB.Shared/Components/UI/SyncMemberStatusButton.razor")]
    [InlineData("src/RTUB.Web/wwwroot/css/3-components/user-card.css")]
    [InlineData("src/RTUB.Web/wwwroot/css/3-components/tracing-card.css")]
    public void TheBlazorPagesAndTheirOwnComponents_AreGone(string file)
    {
        File.Exists(Path.Combine(Root, file)).Should().BeFalse("{0} was retired by task 035", file);
    }

    [Fact]
    public void NoBlazorPage_DeclaresAnOwnerRoute()
    {
        var routes = Directory.GetFiles(Web, "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"@page ""([^""]+)""").Select(m => m.Groups[1].Value));

        routes.Should().NotIntersectWith(Routes);
    }

    [Fact]
    public void TheReactPortal_MapsTheFiveOwnerPages()
    {
        var main = Read(Portal, "main.tsx");
        var program = Read(Web, "Program.cs");

        foreach (var (route, page) in new[]
                 {
                     ("/emails", "OwnerEmails"), ("/notifications", "OwnerNotifications"), ("/users", "OwnerUsers"),
                     ("/owner/tracing", "OwnerTracing"), ("/owner/db", "OwnerDatabase"),
                 })
        {
            main.Should().Contain($"'{route}': lazy(() => import('./{page}')),");
            File.Exists(Path.Combine(Portal, page + ".tsx")).Should().BeTrue();
        }

        program.Should().Contain("app.MapOwnerEndpoints();")
            .And.Contain("new[] { \"/emails\", \"/notifications\", \"/users\", \"/owner/tracing\", \"/owner/db\" }");
    }

    [Fact]
    public void EveryOwnerCall_GoesThroughTheOwnerGate_BeforeTheAntiforgeryCheck()
    {
        var endpoints = Read(Web, "Endpoints", "OwnerEndpoints.cs");

        endpoints.Should().Contain("var owner = app.MapGroup(\"/api/owner\").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(RequireOwner);")
            .And.Contain("var writes = owner.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);")
            .And.Contain("if (!user.IsInRole(\"Owner\"))");
        Regex.Matches(endpoints, @"\bapp\.Map(Get|Post|Put|Delete)\(").Should().BeEmpty("every route hangs off the gated group");
        Regex.Matches(endpoints, @"\bowner\.Map(Post|Put|Delete)\(").Should().BeEmpty("every write needs the antiforgery token");
    }

    [Fact]
    public void TheOwnerPages_SayNothingAboutTheMove_AndLinkNoRemovedModule()
    {
        foreach (var file in Directory.GetFiles(Portal, "Owner*.tsx").Append(Path.Combine(Portal, "ownerApi.ts")))
        {
            var text = File.ReadAllText(file);
            var visible = Regex.Replace(text, @"/\*\*.*?\*/|//[^\n]*", string.Empty, RegexOptions.Singleline);
            visible.Should().NotContainEquivalentOf("migra", "{0} is app-facing", Path.GetFileName(file));
            visible.Should().NotContainEquivalentOf("blazor");
            foreach (var removed in new[] { "/games", "/bets", "/my-tuno", "/messages", "'/images'", "/labels" })
            {
                text.Should().NotContain(removed);
            }
        }
    }

    /// <summary>
    /// #220: every entity type /owner/tracing can show (each audited table, plus the manual audit types) has a
    /// Portuguese label; the excluded messaging types do not need one and must stay excluded.
    /// </summary>
    [Fact]
    public void Tracing_LabelsEveryAuditedType()
    {
        var tracing = Read(Portal, "OwnerTracing.tsx");
        var audited = typeof(RTUB.Application.Data.ApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Where(t => typeof(RTUB.Core.Entities.BaseEntity).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Where(n => !RTUB.Core.Constants.AuditConfiguration.ExcludedEntityTypes.Contains(n))
            .Concat(["ApplicationUser", "UserRole", "Document", "Folder", "Notification", "MemberActivityStatus"])
            .ToList();

        audited.Should().HaveCountGreaterThan(40);
        audited.Where(t => !tracing.Contains($"\n  {t}: '")).Should().BeEmpty("each audited type needs a label in ENTITY_LABELS");
        RTUB.Core.Constants.AuditConfiguration.ExcludedEntityTypes.Should()
            .Contain(new[] { "Message", "Conversation", "ConversationUserSettings", "MessageReaction" });
    }

    /// <summary>
    /// #220 card: the name first (never "ID: n"), then the friendly type with the action and a written "Crítica", then
    /// who and when as dd/MM/yyyy HH:mm, then Detalhes and delete. Details keep every raw field; filters submit raw types.
    /// </summary>
    [Fact]
    public void Tracing_CardLeadsWithTheName_AndDetailsKeepTheRawEvidence()
    {
        var tracing = Read(Portal, "OwnerTracing.tsx");
        var card = tracing[tracing.IndexOf("<ul className=\"owner-logs\">", StringComparison.Ordinal)..tracing.IndexOf("<OwnerPager", StringComparison.Ordinal)];

        string[] order = ["owner-log__name", "owner-log__type", "owner-action", "Crítica", "owner-log__meta", "name=\"eye\"", "icon-btn--danger"];
        order.Select(m => card.IndexOf(m, StringComparison.Ordinal)).Should().BeInAscendingOrder().And.NotContain(-1);
        card.Should().Contain("owner-log--critical").And.NotContain("ID: ");
        tracing.Should().NotContain("`ID: ${");
        tracing.Should().Contain("${two(d.getDate())}/${two(d.getMonth() + 1)}/${d.getFullYear()} ${two(d.getHours())}:${two(d.getMinutes())}");

        var details = tracing[tracing.IndexOf("function DetailsDialog", StringComparison.Ordinal)..tracing.IndexOf("function DeleteDialog", StringComparison.Ordinal)];
        foreach (var raw in new[] { "entry.entityType}", "entry.entityId ??", "entry.entityDisplayName ||", "entry.currentDisplayName}", "entry.userName ??",
                     "entry.action}", "entry.isCritical ?", "pretty(entry.changes)" })
        {
            details.Should().Contain(raw);
        }

        tracing.Should().Contain("<option key={i} value={i}>").And.Contain("text={(type) =>", "the type filter shows labels but submits raw types");
        var css = Read(Web, "portal", "src", "styles.css");
        css.Should().Contain(".owner-log--critical {\n  border-left-color:");
        // Phones: at least a 44 x 44 tap area around the compact delete (36px) and Detalhes (40px tall) buttons.
        // The browser check is the responsive audit's owner-tracing-targets scenario.
        css.Should().Contain("@media (max-width: 767px), (pointer: coarse) {\n  .owner-log__buttons {\n    gap: 10px;\n  }")
            .And.Contain("  .owner-log__buttons .btn::after,\n  .owner-log__buttons .icon-btn::after {\n    position: absolute;\n    inset: -3px;")
            .And.Contain("  .owner-log__buttons .icon-btn::after {\n    inset: -5px;");
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
