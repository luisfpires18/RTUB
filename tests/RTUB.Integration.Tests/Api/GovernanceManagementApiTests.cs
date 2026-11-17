using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Órgãos Sociais management on /api/governance (React track 016, was the Blazor /member/roles) through the real
/// host: real login, antiforgery, SQLite, the existing validation and role services. The RGI link comes from the
/// document storage fake of <see cref="EventsApiFactory"/>: nothing reaches R2.
/// </summary>
public partial class GovernanceManagementApiTests : IClassFixture<EventsApiFactory>
{
    private const string RgiUrl = "https://test-account-id.r2.cloudflarestorage.com/test-bucket/docs/rtub_rgi.pdf?X-Amz-Signature=fake";
    private readonly EventsApiFactory _factory;

    public GovernanceManagementApiTests(EventsApiFactory factory)
    {
        _factory = factory;
        _factory.Documents.Setup(d => d.GetDocumentUrlAsync("docs/rtub_rgi.pdf", false)).ReturnsAsync(RgiUrl);
    }

    [Fact]
    public async Task Visitors_GetNothing_AndMembersGetTheRgiOnly()
    {
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/governance/manage", "/api/governance/members?q=a", "/api/governance/rgi" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await anonymous.PostAsJsonAsync("/api/governance/years", new { startYear = 2001 })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (member, _) = await SignInAsync("Member");
        await WithTokenAsync(member);
        var rgi = await member.GetAsync("/api/governance/rgi");
        rgi.StatusCode.Should().Be(HttpStatusCode.OK);
        rgi.Headers.CacheControl!.NoStore.Should().BeTrue("a pre-signed link is never cached");
        (await rgi.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString().Should().Be(RgiUrl);

        (await member.GetAsync("/api/governance/manage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/governance/members?q=a")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsJsonAsync("/api/governance/years", new { startYear = 2001 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsJsonAsync("/api/governance/assignments", new { fiscalYear = "2001-2002", position = "Magister", userId = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync("/api/governance/assignments/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (roleless, _) = await SignInAsync();
        (await roleless.GetAsync("/api/governance/rgi")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the old page showed the RGI to Member, Mod and Admin only");
    }

    [Theory]
    [InlineData("Mod")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task ModAdminAndOwner_Manage_AndSeeTheRgi(string role)
    {
        var (client, _) = await SignInAsync(role);

        var manage = await client.GetAsync("/api/governance/manage");
        manage.StatusCode.Should().Be(HttpStatusCode.OK, role == "Owner" ? "the Owner inherits Admin (was Mod and Admin)" : "as before");
        (await client.GetAsync("/api/governance/rgi")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        const int start = 1994;
        await DeleteFiscalYearAsync(start);
        await EnsureFiscalYearAsync(1997);
        var (admin, _) = await SignInAsync("Admin");
        var tuno = await AddMemberAsync(MemberCategory.Tuno);

        (await admin.PostAsJsonAsync("/api/governance/years", new { startYear = start })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/governance/assignments", new { fiscalYear = "1997-1998", position = "Magister", userId = tuno.Id }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync("/api/governance/assignments/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await FiscalYearCountAsync(start)).Should().Be(0);
        (await PositionsAsync(tuno.Id)).Should().BeEmpty();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RoleAssignments.AnyAsync(a => a.UserId == tuno.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task MemberSearch_MatchesNameNicknameOrEmail_WithoutReturningPrivateFields_AndSkipsLeitoesAndExpelled()
    {
        var (admin, _) = await SignInAsync("Admin");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var tuno = await AddMemberAsync(MemberCategory.Tuno, nickname: $"Zé {tag}");
        await AddMemberAsync(MemberCategory.Leitao, nickname: $"Leitão {tag}");
        await AddMemberAsync(MemberCategory.Tuno, nickname: $"Fora {tag}", expelled: true);

        var found = await admin.GetFromJsonAsync<JsonElement>($"/api/governance/members?q=ze {tag}");
        found.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetString().Should().Be(tuno.Id);
        found[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "displayName", "fullName", "avatarUrl");
        found.GetRawText().Should().NotContain("@test.com").And.NotContain("phone");

        var byEmail = await admin.GetFromJsonAsync<JsonElement>($"/api/governance/members?q={tuno.UserName}@test");
        byEmail.EnumerateArray().Should().ContainSingle(m => m.GetProperty("id").GetString() == tuno.Id);
        (await admin.GetFromJsonAsync<JsonElement>("/api/governance/members?q=")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task WhenStorageFails_TheRgiIsReportedUnavailable()
    {
        var (member, _) = await SignInAsync("Member");
        _factory.Documents.Setup(d => d.GetDocumentUrlAsync("docs/rtub_rgi.pdf", false)).ThrowsAsync(new IOException("R2 unavailable"));

        var rgi = await member.GetFromJsonAsync<JsonElement>("/api/governance/rgi");

        rgi.GetProperty("url").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ---------- helpers ----------

    private static JsonElement Seat(JsonElement manage, string position) =>
        manage.GetProperty("bodies").EnumerateArray().SelectMany(b => b.GetProperty("positions").EnumerateArray())
            .Single(p => p.GetProperty("position").GetString() == position);

    private async Task<ApplicationUser> AddMemberAsync(MemberCategory category, bool expelled = false, string? nickname = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = "gov" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            PhoneNumber = "919191919",
            Nickname = nickname ?? userName,
            FirstName = "Gov",
            LastName = "Membro",
            Categories = new List<MemberCategory> { category },
            IsExpelled = expelled
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(user, "Member")).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task EnsureFiscalYearAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.FiscalYears.AnyAsync(y => y.StartYear == start))
        {
            db.FiscalYears.Add(FiscalYear.Create(start, start + 1));
            await db.SaveChangesAsync();
        }
    }

    private async Task DeleteFiscalYearAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FiscalYears.Where(y => y.StartYear == start).ExecuteDeleteAsync();
    }

    private async Task ClearPositionAsync(int start, Position position)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RoleAssignments
            .Where(a => a.StartYear == start && a.Position == position).ExecuteDeleteAsync();
    }

    private async Task HoldFromLastYearAsync(string userId, Position position)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(userId))!;
        user.Positions = new List<Position> { position };
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        (await users.RemoveFromRoleAsync(user, "Member")).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(user, "Admin")).Succeeded.Should().BeTrue();
    }

    private async Task<int> FiscalYearCountAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FiscalYears.CountAsync(y => y.StartYear == start);
    }

    private async Task<RoleAssignment?> AssignmentAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RoleAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    private async Task<IList<string>> RolesAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.GetRolesAsync((await users.FindByIdAsync(userId))!);
    }

    private async Task<List<Position>> PositionsAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == userId)).Positions;
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"gov{Guid.NewGuid():N}"[..20], $"10.79.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.80.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
