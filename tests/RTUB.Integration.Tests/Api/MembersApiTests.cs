using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The React members area on /api/members (React track 017, was the Blazor /members and /hierarchy) through the real
/// host: real login, SQLite and the services the old page used. Every test scopes its own members with a unique tag,
/// since the class shares one database.
/// </summary>
public class MembersApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public MembersApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing()
    {
        var anonymous = Anonymous();
        foreach (var path in new[] { "/api/members", "/api/members/active", "/api/members/birthdays", "/api/members/hierarchy", "/api/members/x" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }
    }

    [Fact]
    public async Task Directory_GroupsMembersAndLeitoes_WithTheOldFilters_AndNoContactFields()
    {
        var tag = Tag();
        var tuno = await AddAsync($"{tag} Bravo", MemberCategory.Tuno, yearTuno: DateTime.Now.Year - 1);
        var caloiro = await AddAsync($"{tag} Alfa", MemberCategory.Caloiro);
        var leitao = await AddAsync($"{tag} Leitão", MemberCategory.Leitao);
        var honorario = await AddAsync($"{tag} Honra", MemberCategory.TunoHonorario);
        await AddInstrumentAsync(tuno.Id, InstrumentType.Bandolim);
        var (member, _) = await SignInAsync("Member");

        var page = await member.GetAsync($"/api/members?q={tag}");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Headers.CacheControl!.NoStore.Should().BeTrue();
        var raw = await page.Content.ReadAsStringAsync();
        raw.Should().NotContain("@test.com").And.NotContain("919191919").And.NotContain("Bragança-Cidade")
            .And.NotContainAny(new[] { "email", "phone", "birth", "city", "passwordHash", "lastLogin\"" });
        var body = JsonDocument.Parse(raw).RootElement;
        Names(body, "members").Should().Equal(new[] { $"{tag} Alfa", $"{tag} Bravo", $"{tag} Honra" },
            "by nickname; a search also finds Tunos Honorários, as before");
        Names(body, "leitoes").Should().Equal($"{tag} Leitão");
        var bravo = body.GetProperty("members")[1];
        bravo.GetProperty("instrument").GetString().Should().Be("Bandolim");
        bravo.GetProperty("badges").EnumerateArray().Select(b => b.GetProperty("label").GetString()).Should().Equal("TUNO");
        body.GetProperty("leitoes")[0].GetProperty("inactive").GetBoolean().Should().BeTrue("no activity this month");
        body.GetProperty("canManage").GetBoolean().Should().BeFalse();

        Names(await Json(member, $"/api/members?q={tag}&category=Caloiro"), "members").Should().Equal($"{tag} Alfa");
        Names(await Json(member, $"/api/members?q={tag}&category=TunoHonorario"), "members").Should().Equal($"{tag} Honra");
        Names(await Json(member, $"/api/members?q={tag}&category=Leitao"), "leitoes").Should().Equal($"{tag} Leitão");
        Names(await Json(member, $"/api/members?q={tag}&instrument=Bandolim"), "members").Should().Equal($"{tag} Bravo");
        Names(await Json(member, $"/api/members?q={tag}&category=Tuno&subCategory=Tuno"), "members").Should().Equal($"{tag} Bravo");
        Names(await Json(member, $"/api/members?q={tuno.UserName}@test"), "members").Should().Equal(new[] { $"{tag} Bravo" },
            "the search matches the email, as before, without returning it");

        var all = await Json(member, "/api/members");
        all.GetProperty("members").EnumerateArray().Should().NotContain(m => m.GetProperty("id").GetString() == honorario.Id,
            "with no filter or search, Tunos Honorários stay out of the general view");

        foreach (var bad in new[] { "category=Nope", "instrument=Kazoo", "category=Tuno&subCategory=Leitao" })
        {
            (await member.GetAsync($"/api/members?{bad}")).StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);
        }

        _ = caloiro;
        _ = leitao;
    }

    [Theory]
    [InlineData("Member", false)]
    [InlineData("Mod", false)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public async Task OnlyAdminAndOwner_GetTheLinkToTheManagementTools(string role, bool canManage)
    {
        var (client, _) = await SignInAsync(role);

        (await Json(client, "/api/members?q=zzz-none")).GetProperty("canManage").GetBoolean().Should().Be(canManage);
    }

    [Fact]
    public async Task Details_ShowWhatTheOldModalShowedAnyMember()
    {
        var tag = Tag();
        var padrinho = await AddAsync($"{tag} Padrinho", MemberCategory.Tuno, yearTuno: 2010);
        var afilhado = await AddAsync($"{tag} Afilhado", MemberCategory.Tuno, yearTuno: DateTime.Now.Year - 3, mentorId: padrinho.Id,
            yearLeitao: DateTime.Now.Year - 5, yearCaloiro: DateTime.Now.Year - 4);
        var honorario = await AddAsync($"{tag} Honra", MemberCategory.TunoHonorario, mentorId: padrinho.Id);
        await AddInstrumentAsync(afilhado.Id, InstrumentType.Guitarra);
        var (member, _) = await SignInAsync("Member");

        var detail = await Json(member, $"/api/members/{afilhado.Id}");
        detail.GetProperty("email").GetString().Should().Be(afilhado.Email, "the old modal showed contacts to any signed-in member");
        detail.GetProperty("phone").GetString().Should().Be("919191919");
        detail.GetProperty("city").GetString().Should().Be("Bragança-Cidade");
        detail.GetProperty("birthDate").GetString().Should().Be("01/02/2000");
        detail.GetProperty("mentor").GetString().Should().Be($"{tag} Padrinho (Mem Bro)", "the padrinho's display name, as the old modal");
        detail.GetProperty("instruments").EnumerateArray().Select(i => i.GetString()).Should().Equal("Guitarra (Principal)");
        detail.GetProperty("badges").EnumerateArray().Select(b => b.GetProperty("label").GetString()).Should().Equal("TUNO", "VETERANO");
        detail.GetProperty("timeline").EnumerateArray().Select(t => t.GetProperty("label").GetString())
            .Should().Equal("LEITÃO", "CALOIRO", "TUNO", "VETERANO");
        detail.GetRawText().Should().NotContainAny("passwordHash", "securityStamp", "userName");

        var honra = await Json(member, $"/api/members/{honorario.Id}");
        honra.GetProperty("showInstruments").GetBoolean().Should().BeFalse();
        honra.GetProperty("showMentor").GetBoolean().Should().BeFalse("no Padrinho for a Tuno Honorário");
        honra.GetProperty("state").ValueKind.Should().Be(JsonValueKind.Null);

        (await member.GetAsync("/api/members/no-such-member")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hierarchy_NestsAfilhadosUnderTunoPadrinhos_ByCaloiroDate_WithoutLeitoes()
    {
        var tag = Tag();
        var root = await AddAsync($"{tag} Raiz", MemberCategory.Tuno, yearTuno: 2000);
        await AddAsync($"{tag} Segundo", MemberCategory.Caloiro, mentorId: root.Id, yearCaloiro: 2024);
        var first = await AddAsync($"{tag} Primeiro", MemberCategory.Tuno, mentorId: root.Id, yearCaloiro: 2019, yearTuno: 2020);
        await AddAsync($"{tag} Neto", MemberCategory.Caloiro, mentorId: first.Id, yearCaloiro: 2025);
        await AddAsync($"{tag} Leitão", MemberCategory.Leitao, mentorId: root.Id);
        var caloiroMentor = await AddAsync($"{tag} Caloiro", MemberCategory.Caloiro);
        await AddAsync($"{tag} Sem Padrinho Tuno", MemberCategory.Caloiro, mentorId: caloiroMentor.Id);
        var (member, _) = await SignInAsync("Member");

        var tree = await Json(member, "/api/members/hierarchy");
        var raiz = tree.EnumerateArray().Single(n => n.GetProperty("id").GetString() == root.Id);
        raiz.GetProperty("children").EnumerateArray().Select(c => c.GetProperty("displayName").GetString())
            .Should().Equal($"{tag} Primeiro", $"{tag} Segundo");
        raiz.GetProperty("children")[0].GetProperty("children")[0].GetProperty("displayName").GetString().Should().Be($"{tag} Neto");
        tree.GetRawText().Should().NotContain($"{tag} Leitão", "Leitões are not in the family tree");
        tree.EnumerateArray().Select(n => n.GetProperty("displayName").GetString()).Should().Contain($"{tag} Sem Padrinho Tuno",
            "a Caloiro is never a padrinho: the afilhado stands as a root");
    }

    [Fact]
    public async Task ActiveMembers_AreCaloirosAndTunos_WithTheStatusFilter()
    {
        var tag = Tag();
        var active = await AddAsync($"{tag} Ativo", MemberCategory.Tuno, yearTuno: 2020);
        var retired = await AddAsync($"{tag} Reformado", MemberCategory.Caloiro, retired: true);
        await AddAsync($"{tag} Leitão", MemberCategory.Leitao);
        await AddAsync($"{tag} Honra", MemberCategory.TunoHonorario);
        var (member, _) = await SignInAsync("Member");

        var list = await Json(member, $"/api/members/active?q={Uri.EscapeDataString(tag)}");
        list.EnumerateArray().Select(m => m.GetProperty("displayName").GetString()).Should().Equal(new[] { $"{tag} Ativo", $"{tag} Reformado" },
            "active first, never Leitões or Tunos Honorários");
        list.GetRawText().Should().NotContain("@test.com");
        (await Json(member, $"/api/members/active?status=retired&q={Uri.EscapeDataString(tag)}")).EnumerateArray()
            .Select(m => m.GetProperty("id").GetString()).Should().Equal(retired.Id);
        (await member.GetAsync("/api/members/active?status=sleeping")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _ = active;
    }

    [Fact]
    public async Task Birthdays_AreTheOnesStillToComeThisYear_SoonestFirst_WithoutTheBirthYear()
    {
        var tag = Tag();
        var today = DateTime.Now.Date;
        var later = today.AddDays(1).Year == today.Year ? today.AddDays(1) : today;
        await AddAsync($"{tag} Depois", MemberCategory.Tuno, birth: new DateTime(1990, later.Month, later.Day));
        await AddAsync($"{tag} Hoje", MemberCategory.Tuno, birth: new DateTime(1995, today.Month, today.Day));
        if (today.DayOfYear > 1)
        {
            var before = today.AddDays(-1);
            await AddAsync($"{tag} Passou", MemberCategory.Tuno, birth: new DateTime(1993, before.Month, before.Day));
        }

        var (member, _) = await SignInAsync("Member");
        var list = await Json(member, $"/api/members/birthdays?q={Uri.EscapeDataString(tag)}");

        var names = list.EnumerateArray().Select(b => b.GetProperty("displayName").GetString()).ToList();
        names.Should().NotContain($"{tag} Passou");
        names.First().Should().Be($"{tag} Hoje");
        var hoje = list[0];
        hoje.GetProperty("age").GetInt32().Should().Be(today.Year - 1995);
        hoje.GetProperty("birthday").GetString().Should().Be(today.ToString("dd/MM"));
        list.GetRawText().Should().NotContain("1995").And.NotContain("dateOfBirth").And.NotContain("@test.com");
    }

    [Fact]
    public async Task Routes_AreReact_TheOldOnesRedirect_AndTheToolsAreForAdminAndOwnerOnly()
    {
        var (member, _) = await SignInAsync("Member");
        var (admin, _) = await SignInAsync("Admin");

        foreach (var path in new[] { "/members", "/members/hierarchy" })
        {
            var html = await member.GetStringAsync(path);
            html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js", path);
            var visitor = await Anonymous().GetAsync(path);
            visitor.StatusCode.Should().Be(HttpStatusCode.Redirect);
            visitor.Headers.Location!.ToString().Should().StartWith("/login?returnUrl=");
        }

        (await Anonymous().GetAsync("/hierarchy")).Headers.Location!.ToString().Should().Be("/members/hierarchy");

        (await member.GetAsync("/members/manage")).StatusCode.Should().Be(HttpStatusCode.Redirect, "the tools are Admin/Owner only now");
        var tools = await admin.GetAsync("/members/manage");
        tools.StatusCode.Should().Be(HttpStatusCode.OK);
        (await tools.Content.ReadAsStringAsync()).Should().Contain("blazor.web.js", "the admin bridge stays Blazor for now");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Members.Hierarchy").Should().BeNull("the Blazor hierarchy page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/members", "/hierarchy", "/members/hierarchy" }, "no Blazor page owns the React member routes");
    }

    [Fact]
    public void Links_PointAtTheReactMembersArea()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("members: '/members'").And.Contain("membersHierarchy: '/members/hierarchy'");
        File.ReadAllText(Path.Combine(src, "Profile.tsx")).Should().Contain("href={portal.members}");
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "Shared", "MainLayout.razor"))
            .Should().Contain("href=\"/members/hierarchy\"").And.NotContain("href=\"/hierarchy\"");
        foreach (var file in new[] { "Members.tsx", "MembersHierarchy.tsx", "MemberDialogs.tsx", "membersApi.ts" })
        {
            File.ReadAllText(Path.Combine(src, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }
    }

    // ---------- helpers ----------

    private static string Tag() => "m" + Guid.NewGuid().ToString("N")[..8];

    private static IEnumerable<string?> Names(JsonElement body, string group) =>
        body.GetProperty(group).EnumerateArray().Select(m => m.GetProperty("displayName").GetString());

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<ApplicationUser> AddAsync(string nickname, MemberCategory category, int? yearTuno = null, string? mentorId = null,
        int? yearLeitao = null, int? yearCaloiro = null, bool retired = false, DateTime? birth = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = "mem" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            PhoneNumber = "919191919",
            City = "Bragança-Cidade",
            DateOfBirth = birth ?? new DateTime(2000, 2, 1),
            Nickname = nickname,
            FirstName = "Mem",
            LastName = "Bro",
            Categories = new List<MemberCategory> { category },
            YearTuno = yearTuno ?? (category == MemberCategory.TunoHonorario ? 2015 : null),
            MonthTuno = yearTuno is null ? null : 1,
            YearLeitao = yearLeitao,
            YearCaloiro = yearCaloiro,
            MonthCaloiro = yearCaloiro is null ? null : 3,
            MentorId = mentorId,
            IsRetired = retired
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task AddInstrumentAsync(string userId, InstrumentType instrument)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, instrument, isPrimary: true));
        await db.SaveChangesAsync();
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"mem{Guid.NewGuid():N}"[..20], $"10.81.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.82.0.1");
        return client;
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
