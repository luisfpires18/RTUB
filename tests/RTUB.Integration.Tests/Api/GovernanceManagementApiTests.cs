using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Órgãos Sociais management on /api/governance (React track 016, was the Blazor /member/roles) through the real
/// host: real login, antiforgery, SQLite, the existing validation and role services. The RGI link comes from the
/// document storage fake of <see cref="EventsApiFactory"/>: nothing reaches R2.
/// </summary>
public class GovernanceManagementApiTests : IClassFixture<EventsApiFactory>
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
    public async Task FiscalYears_AreCreatedOnce_FromThe1991UpToTheCurrentOne()
    {
        const int start = 1992;
        await DeleteFiscalYearAsync(start);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        var before = await admin.GetFromJsonAsync<JsonElement>("/api/governance/manage");
        before.GetProperty("availableStartYears").EnumerateArray().Select(y => y.GetInt32()).Should().Contain(start);

        var created = await admin.PostAsJsonAsync("/api/governance/years", new { startYear = start });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("fiscalYear").GetString().Should().Be("1992-1993", "the new year is shown");
        body.GetProperty("availableStartYears").EnumerateArray().Select(y => y.GetInt32()).Should().NotContain(start);
        body.GetProperty("bodies").GetArrayLength().Should().Be(5);
        (await FiscalYearCountAsync(start)).Should().Be(1);

        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        foreach (var refused in new[] { start, current + 1, 1990 })
        {
            var response = await admin.PostAsJsonAsync("/api/governance/years", new { startYear = refused });
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("startYear", out _).Should().BeTrue();
        }

        (await FiscalYearCountAsync(start)).Should().Be(1, "no duplicate");
        (await FiscalYearCountAsync(current + 1)).Should().Be(0, "no future year");
    }

    [Fact]
    public async Task Assignments_FollowTheOldRules_AndRemoveByIdOnce()
    {
        const int start = 1996;
        await EnsureFiscalYearAsync(start);
        var (mod, me) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var tuno = await AddMemberAsync(MemberCategory.Tuno);
        var other = await AddMemberAsync(MemberCategory.Tuno);
        var caloiro = await AddMemberAsync(MemberCategory.Caloiro);
        var leitao = await AddMemberAsync(MemberCategory.Leitao);
        var expelled = await AddMemberAsync(MemberCategory.Tuno, expelled: true);
        const string year = "1996-1997";

        var assigned = await mod.PostAsJsonAsync("/api/governance/assignments", new { fiscalYear = year, position = "Magister", userId = tuno.Id });
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);
        var magister = Seat(await assigned.Content.ReadFromJsonAsync<JsonElement>(), "Magister");
        magister.GetProperty("title").GetString().Should().Be("Magister");
        var holder = magister.GetProperty("holders").EnumerateArray().Single();
        holder.GetProperty("displayName").GetString().Should().Be(tuno.Nickname);
        holder.GetRawText().Should().NotContain("@test.com").And.NotContain("userId");
        var saved = await AssignmentAsync(holder.GetProperty("assignmentId").GetInt32());
        (saved!.UserId, saved.StartYear, saved.EndYear, saved.CreatedBy).Should().Be((tuno.Id, start, start + 1, me.UserName), "the audit stamps who assigned");

        var publicView = await Anonymous().GetFromJsonAsync<JsonElement>($"/api/public/governance?fiscalYear={year}");
        publicView.GetProperty("bodies")[0].GetProperty("positions")[0].GetProperty("holders")[0].GetProperty("displayName").GetString()
            .Should().Be(tuno.Nickname, "the public view shows it at once");

        async Task Refused(string? fiscalYear, string? position, string? userId, string field)
        {
            var response = await mod.PostAsJsonAsync("/api/governance/assignments", new { fiscalYear, position, userId });
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{fiscalYear}/{position}/{userId}");
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue(field);
        }

        await Refused(year, "Magister", other.Id, "assignment");           // the position is taken
        await Refused(year, "Secretario", tuno.Id, "assignment");          // one position per member
        await Refused(year, "PresidenteMesaAssembleia", caloiro.Id, "assignment"); // no Caloiro president
        await Refused(year, "Secretario", leitao.Id, "assignment");        // no Leitão
        await Refused(year, "Secretario", expelled.Id, "userId");          // expelled members are refused
        await Refused(year, "Secretario", "no-such-user", "userId");
        await Refused(year, "3", other.Id, "position");                    // names only
        await Refused(year, "Presidente", other.Id, "position");
        await Refused("1850-1851", "Secretario", other.Id, "fiscalYear");  // not a created year
        await Refused("1996", "Secretario", other.Id, "fiscalYear");

        (await mod.PostAsJsonAsync("/api/governance/assignments", new { fiscalYear = year, position = "Ensaiador", userId = tuno.Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "Ensaiador is not an official member role and can be held alongside");

        var assignmentId = holder.GetProperty("assignmentId").GetInt32();
        var removed = await mod.DeleteAsync($"/api/governance/assignments/{assignmentId}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        Seat(await removed.Content.ReadFromJsonAsync<JsonElement>(), "Magister").GetProperty("holders").GetArrayLength().Should().Be(0);
        (await AssignmentAsync(assignmentId)).Should().BeNull();
        (await mod.DeleteAsync($"/api/governance/assignments/{assignmentId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InTheCurrentFiscalYear_AssignAndRemove_UpdateTheMembersRole_AsBefore()
    {
        var start = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        await EnsureFiscalYearAsync(start);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        var tuno = await AddMemberAsync(MemberCategory.Tuno);
        await ClearPositionAsync(start, Position.SegundoTesoureiro);

        var assigned = await owner.PostAsJsonAsync("/api/governance/assignments",
            new { fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString(), position = "SegundoTesoureiro", userId = tuno.Id });
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesAsync(tuno.Id)).Should().BeEquivalentTo("Admin");
        (await PositionsAsync(tuno.Id)).Should().Equal(Position.SegundoTesoureiro);

        var id = Seat(await assigned.Content.ReadFromJsonAsync<JsonElement>(), "SegundoTesoureiro").GetProperty("holders")[0].GetProperty("assignmentId").GetInt32();
        (await owner.DeleteAsync($"/api/governance/assignments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesAsync(tuno.Id)).Should().BeEquivalentTo("Member");
        (await PositionsAsync(tuno.Id)).Should().BeEmpty();
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
