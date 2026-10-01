using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// The public Órgãos Sociais (React track 008): GET /api/public/governance over the wire, the React
/// /roles, and the members' Blazor /member/roles that kept the RGI and the management tools.
/// The grouping and mandate rules are pinned by GovernanceServiceTests; here, what anonymous
/// readers actually receive.
/// </summary>
public class GovernanceTests : IntegrationTestBase
{
    private const string PrivateEmail = "governance-private@test.com";
    private const string PrivatePhone = "919191919";
    private const string PrivateNote = "internal-governance-note";

    public GovernanceTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Api_IsAnonymous_EmptyUntilSomeoneIsAssigned_ThenGroupsThePublicFieldsOnly()
    {
        var client = Factory.CreateClient();

        // This class's database starts with no positions recorded.
        var empty = await GetJsonAsync(client, "/api/public/governance");
        empty.GetProperty("fiscalYears").GetArrayLength().Should().Be(0);
        empty.GetProperty("fiscalYear").ValueKind.Should().Be(JsonValueKind.Null);
        empty.GetProperty("bodies").GetArrayLength().Should().Be(0);

        var start = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        await AssignAsync(Position.Magister, start, nickname: "Magister Tuno", imageUrl: "https://pub-test.r2.dev/a.webp");
        await AssignAsync(Position.PresidenteConselhoFiscal, start, nickname: "Sem Foto", imageUrl: null);

        var response = await client.GetAsync("/api/public/governance");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoCache.Should().BeTrue("a new assignment must show at once");

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(PrivateEmail).And.NotContain(PrivatePhone).And.NotContain(PrivateNote)
            .And.NotContain("2000-01-01", "no birth date")
            .And.NotContainAny(new[] { "\"id\"", "userId", "email", "phone", "birth", "notes", "createdBy", "userName" });

        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("fiscalYear").GetString().Should().Be($"{start}-{start + 1}");

        var direction = body.GetProperty("bodies")[0];
        direction.GetProperty("name").GetString().Should().Be("Direção");
        var magister = direction.GetProperty("positions")[0];
        magister.GetProperty("title").GetString().Should().Be("Magister");
        var holder = magister.GetProperty("holders")[0];
        holder.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("displayName", "fullName", "avatarUrl");
        holder.GetProperty("displayName").GetString().Should().Be("Magister Tuno");
        holder.GetProperty("avatarUrl").GetString().Should().Be("https://pub-test.r2.dev/a.webp");

        var fiscalPresident = body.GetProperty("bodies")[2].GetProperty("positions")[0].GetProperty("holders")[0];
        fiscalPresident.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null,
            "no photo means none is published; the page shows the default avatar");
    }

    [Fact]
    public async Task Api_IsReadOnly()
    {
        var response = await Factory.CreateClient().PostAsync("/api/public/governance", null);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Roles_IsTheReactShell()
    {
        var html = await Factory.CreateClient().GetStringAsync("/roles?fy=2024-2025");

        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    [Fact]
    public async Task MembersGovernance_NeedsSignIn_AndKeepsTheRgiAndManagement()
    {
        var response = await Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/member/roles");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login");

        var page = typeof(RTUB.App).Assembly.GetType("RTUB.Pages.Members.MemberGovernance");
        page.Should().NotBeNull("the RGI and the Mod/Admin tools stay on the members' Blazor page");
        page!.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Single().Template.Should().Be("/member/roles");
        page.GetCustomAttributes(typeof(AuthorizeAttribute), false).Should().NotBeEmpty();
    }

    [Fact]
    public void PortalLinks_PointAtTheReactRolesPage()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var content = File.ReadAllText(Path.Combine(src, "content.ts"));

        content.Should().Contain("roles: '/roles'").And.Contain("memberGovernance: '/member/roles'");
        File.ReadAllText(Path.Combine(src, "Home.tsx")).Should().Contain("<MoreLink href={portal.roles}>",
            "the home Órgãos Sociais section opens the React page");
        File.ReadAllText(Path.Combine(src, "App.tsx")).Should().Contain("governance: portal.roles",
            "the top bar, menu and footer open /roles, not the home anchor");
        Directory.GetFiles(src).Select(File.ReadAllText).Should().NotContain(t => t.Contains("legacy.roles"));
    }

    // ---------- helpers ----------

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private async Task AssignAsync(Position position, int startYear, string nickname, string? imageUrl)
    {
        using var scope = Factory.Services.CreateScope();
        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await contexts.CreateDbContextAsync();

        var user = new ApplicationUser
        {
            UserName = "gov-" + Guid.NewGuid().ToString("N")[..10],
            Email = PrivateEmail,
            PhoneNumber = PrivatePhone,
            Nickname = nickname,
            FirstName = "Primeiro",
            LastName = "Último",
            ImageUrl = imageUrl,
            DateOfBirth = new DateTime(2000, 1, 1)
        };
        db.Users.Add(user);
        db.RoleAssignments.Add(RoleAssignment.Create(user.Id, position, startYear, startYear + 1, notes: PrivateNote));
        await db.SaveChangesAsync();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
