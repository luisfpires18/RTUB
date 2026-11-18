// MembersApiTests: Details tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class MembersApiTests
{
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
        // Active only with recent activity: since 044B a flag with nothing in six months is reformed on the next read.
        await AttendAsync(active.Id, DateTime.Today.AddDays(-7));
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
    public async Task Routes_AreReact()
    {
        var (member, _) = await SignInAsync("Member");

        foreach (var path in new[] { "/members", "/members/hierarchy" })
        {
            var html = await member.GetStringAsync(path);
            html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js", path);
            var visitor = await Anonymous().GetAsync(path);
            visitor.StatusCode.Should().Be(HttpStatusCode.Redirect);
            visitor.Headers.Location!.ToString().Should().StartWith("/login?returnUrl=");
        }

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Members.Hierarchy").Should().BeNull("the Blazor hierarchy page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/members", "/hierarchy", "/members/hierarchy", "/members/manage" }, "no Blazor page owns the React member routes");
    }

    [Fact]
    public void Links_PointAtTheReactMembersArea()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "data/content.ts")).Should().Contain("members: '/members'").And.Contain("membersHierarchy: '/members/hierarchy'");
        File.ReadAllText(Path.Combine(src, "components/MemberShell.tsx")).Should().Contain("href: portal.members,").And.Contain("href: portal.membersHierarchy,");
        // MemberManage.tsx was split (113) into the Member*Dialog / MemberFormFields files and data/memberFormState.ts.
        var memberFiles = new[] { "pages/Members.tsx", "pages/MembersHierarchy.tsx", "components/MemberDialogs.tsx", "components/MemberFormDialog.tsx",
            "components/MemberFormFields.tsx", "components/MemberInfoDialog.tsx", "components/MemberActionDialogs.tsx", "data/memberFormState.ts", "data/membersApi.ts" };
        foreach (var file in memberFiles)
        {
            File.ReadAllText(Path.Combine(src, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }
    }
}
