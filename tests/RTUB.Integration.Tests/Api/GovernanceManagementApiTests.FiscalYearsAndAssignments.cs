// GovernanceManagementApiTests: FiscalYearsAndAssignments tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class GovernanceManagementApiTests
{
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
    public async Task ANewMandate_ReplacesLastYearsHolders_PositionsAndRoles()
    {
        var start = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        await EnsureFiscalYearAsync(start);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        var leaving = await AddMemberAsync(MemberCategory.Tuno);
        var reElected = await AddMemberAsync(MemberCategory.Tuno);
        var newcomer = await AddMemberAsync(MemberCategory.Tuno);
        await HoldFromLastYearAsync(leaving.Id, Position.Magister);
        await HoldFromLastYearAsync(reElected.Id, Position.ViceMagister);
        await ClearPositionAsync(start, Position.PrimeiroTesoureiro);
        await ClearPositionAsync(start, Position.Secretario);

        (await owner.PostAsJsonAsync("/api/governance/assignments",
            new { fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString(), position = "Secretario", userId = reElected.Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsJsonAsync("/api/governance/assignments",
            new { fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString(), position = "PrimeiroTesoureiro", userId = newcomer.Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await RolesAsync(leaving.Id)).Should().BeEquivalentTo(new[] { "Member" }, "last year's seat is gone");
        (await PositionsAsync(leaving.Id)).Should().BeEmpty();
        (await RolesAsync(reElected.Id)).Should().BeEquivalentTo("Admin");
        (await PositionsAsync(reElected.Id)).Should().Equal(Position.Secretario);
        (await RolesAsync(newcomer.Id)).Should().BeEquivalentTo("Admin");
        (await PositionsAsync(newcomer.Id)).Should().Equal(Position.PrimeiroTesoureiro);

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IGovernanceManagementService>().SyncCurrentMandateAsync())
            .Should().Be(0, "idempotent");
    }
}
