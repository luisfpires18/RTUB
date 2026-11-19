// PendingActionsApiTests tests: member information and the members queue.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class PendingActionsApiTests
{
    // ---------- Membros: the Mod's limited edit and queue (076) ----------

    [Fact]
    public async Task AMod_CompletesMemberInformation_ButNothingElse()
    {
        var (mod, modUser) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Mod");
        var (plain, _) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await AreaHttp.WithTokenAsync(mod);
        await AreaHttp.WithTokenAsync(plain);
        var leitao = await AddMemberAsync([MemberCategory.Leitao], city: null, yearLeitao: null);

        (await AllKeysAsync(mod, "members", null)).Should().Contain(leitao);
        (await plain.GetAsync("/api/pending-actions/panels/members")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a Member with the same category");
        var (owner, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Owner");
        (await owner.GetAsync("/api/pending-actions/panels/members")).StatusCode.Should().Be(HttpStatusCode.NotFound, "087: the Owner has no Membros panel");
        (await plain.GetAsync($"/api/members/{leitao}/info")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await plain.PostAsJsonAsync($"/api/members/{leitao}/instruments", new { instrument = "Guitarra", primary = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var info = await mod.GetFromJsonAsync<JsonElement>($"/api/members/{leitao}/info");
        info.GetProperty("fields").EnumerateArray().Select(f => f.GetString()).Should().Equal("city", "phone", "instrument", "leitao", "mentor");
        info.GetProperty("missing").EnumerateArray().Select(f => f.GetString()).Should().Contain(["city", "leitao", "instrument", "mentor"]);

        // A Caloiro date on a Leitão is refused; forged extra fields are not part of the contract and change nothing.
        var stamp = info.GetProperty("stamp").GetString();
        (await mod.PutAsJsonAsync($"/api/members/{leitao}/info", new { city = "Braga", phoneNumber = "912345678", yearLeitao = 2026, monthLeitao = 10,
            yearCaloiro = 2026, monthCaloiro = 11, stamp })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var saved = await mod.PutAsJsonAsync($"/api/members/{leitao}/info", new
        {
            city = "Braga", phoneNumber = "912345678", yearLeitao = 2026, monthLeitao = 10, stamp,
            categories = new[] { "Tuno" }, category = "Tuno", email = "forjado@test.com", isExpelled = true, isRetired = false, roles = new[] { "Owner" },
            firstName = "Forjado", positions = new[] { "Magister" },
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PostAsJsonAsync($"/api/members/{leitao}/instruments", new { instrument = "Guitarra", primary = true })).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var m = await db.Users.AsNoTracking().SingleAsync(u => u.Id == leitao);
            (m.City, m.YearLeitao, m.MonthLeitao).Should().Be(("Braga", 2026, 10));
            m.Categories.Should().Equal(MemberCategory.Leitao);
            m.Email.Should().NotBe("forjado@test.com");
            m.FirstName.Should().NotBe("Forjado");
            m.IsExpelled.Should().BeFalse();
            m.Positions.Should().BeEmpty();
            var roles = await db.UserRoles.Where(r => r.UserId == leitao).Join(db.Roles, r => r.RoleId, r => r.Id, (_, r) => r.Name).ToListAsync();
            roles.Should().NotContain("Owner");
            (await db.AuditLogs.AnyAsync(l => l.EntityType == nameof(ApplicationUser) && l.UserName == modUser.UserName)).Should().BeTrue("audited with the Mod as actor");
        }

        // No other Admin power: the full edit, create, delete, nickname, expel.
        (await mod.PutAsJsonAsync($"/api/members/{leitao}", new { firstName = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.DeleteAsync($"/api/members/{leitao}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.PostAsync($"/api/members/{leitao}/expel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.GetAsync($"/api/members/{leitao}/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var after = await mod.GetFromJsonAsync<JsonElement>($"/api/members/{leitao}/info");
        after.GetProperty("missing").EnumerateArray().Select(f => f.GetString()).Should().Equal("mentor");
    }

    [Fact]
    public async Task AMod_FillsMissingDates_ButNotACompletePair_AndAStaleSaveIsRefused()
    {
        var (mod, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Mod");
        await AreaHttp.WithTokenAsync(mod);
        var tuno = await AddMemberAsync([MemberCategory.Tuno], city: "Porto", yearLeitao: 2015, monthLeitao: 10, yearTuno: null);

        var info = await mod.GetFromJsonAsync<JsonElement>($"/api/members/{tuno}/info");
        info.GetProperty("lockedDates").GetProperty("leitao").GetBoolean().Should().BeTrue();
        var stamp = info.GetProperty("stamp").GetString();
        object Body(int? yearLeitao, int? yearTuno, string? s) => new
        {
            city = "Porto", phoneNumber = "912345678", yearLeitao, monthLeitao = 10, yearTuno, monthTuno = yearTuno is null ? (int?)null : 9, stamp = s,
        };

        (await mod.PutAsJsonAsync($"/api/members/{tuno}/info", Body(2014, 2017, stamp))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a complete pair stays Owner / Magister only");
        (await mod.PutAsJsonAsync($"/api/members/{tuno}/info", Body(2015, 2017, stamp))).StatusCode.Should().Be(HttpStatusCode.OK, "a missing date is filled");
        (await mod.PutAsJsonAsync($"/api/members/{tuno}/info", Body(2015, 2017, stamp))).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the stamp read before is stale: no silent overwrite");
    }

    /// <summary>Security review of PR #277: a Mod who is also the current Magister still cannot change a complete date pair.</summary>
    [Fact]
    public async Task AModMagister_CannotChangeACompleteDatePair()
    {
        var (mod, modUser) = await SignInAsync([MemberCategory.Caloiro], retired: false, role: "Mod", positions: [Position.Magister]);
        await AddMagisterAssignmentAsync(modUser.Id);
        await AreaHttp.WithTokenAsync(mod);
        var tuno = await AddMemberAsync([MemberCategory.Tuno], city: "Porto", yearLeitao: 2015, monthLeitao: 10, yearTuno: 2018, monthTuno: 6);

        var info = await mod.GetFromJsonAsync<JsonElement>($"/api/members/{tuno}/info");
        info.GetProperty("lockedDates").GetProperty("leitao").GetBoolean().Should().BeTrue("Owner, or an Admin who is the Magister");
        var refused = await mod.PutAsJsonAsync($"/api/members/{tuno}/info", new
        {
            city = "Porto", phoneNumber = "912345678", yearLeitao = 2014, monthLeitao = 10, yearTuno = 2018, monthTuno = 6,
            stamp = info.GetProperty("stamp").GetString(),
        });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("leitao")[0].GetString();
        message.Should().NotContainAny(["Owner", "Admin", "Mod"], "member-facing text names no system role (PR #277 review)");
    }

    [Fact]
    public async Task TheMembersQueue_LeavesOutTheExpelled_AndWhatDoesNotApply()
    {
        var (admin, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin");
        var fundador = await AddMemberAsync([MemberCategory.Tuno, MemberCategory.Fundador], city: "Braga", yearTuno: 1991, monthTuno: 12, instrument: true);
        var honorario = await AddMemberAsync([MemberCategory.Tuno, MemberCategory.TunoHonorario], city: "Braga", yearTuno: 2020, monthTuno: 5);
        var expelled = await AddMemberAsync([MemberCategory.Leitao], city: null, expelled: true);
        var leitaoComplete = await AddMemberAsync([MemberCategory.Leitao], city: "Braga", yearLeitao: 2026, monthLeitao: 10, instrument: true, mentor: true);
        var blankCity = await AddMemberAsync([MemberCategory.Leitao], city: "   ", yearLeitao: 2026, monthLeitao: 10, instrument: true, mentor: true);

        var keys = await AllKeysAsync(admin, "members", null);
        keys.Should().NotContain([fundador, honorario, expelled, leitaoComplete],
            "nothing missing that applies (no padrinho / Leitão date for Fundador or Honorário, no instrument for Honorário, no Caloiro date for a Leitão), or expelled");
        keys.Should().Contain(blankCity, "a whitespace-only city is missing, as in the edit form (PR #277 review)");
    }

    private async Task AddMagisterAssignmentAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fy = CurrentFiscalYearStart();
        db.RoleAssignments.Add(RoleAssignment.Create(userId, Position.Magister, fy, fy + 1));
        await db.SaveChangesAsync();
    }

    private async Task<string> AddMemberAsync(List<MemberCategory> categories, string? city, int? yearLeitao = null, int? monthLeitao = null,
        int? yearTuno = null, int? monthTuno = null, bool expelled = false, bool instrument = false, bool mentor = false)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var name = $"mi{Guid.NewGuid():N}"[..20];
        string? mentorId = null;
        if (mentor)
        {
            var padrinho = new ApplicationUser
            {
                UserName = $"pd{Guid.NewGuid():N}"[..20], Email = $"pd{Guid.NewGuid():N}"[..20] + "@test.com", FirstName = "Pad", LastName = "Rinho",
                Nickname = "Padrinho", PhoneNumber = "910000000", Categories = [MemberCategory.Tuno], YearTuno = 2010, MonthTuno = 1,
            };
            (await users.CreateAsync(padrinho)).Succeeded.Should().BeTrue();
            mentorId = padrinho.Id;
        }

        var user = new ApplicationUser
        {
            UserName = name, Email = $"{name}@test.com", FirstName = "Ana", LastName = "Silva", Nickname = name, PhoneNumber = "912345678",
            City = city, Categories = categories, YearLeitao = yearLeitao, MonthLeitao = monthLeitao, YearTuno = yearTuno, MonthTuno = monthTuno,
            IsExpelled = expelled, MentorId = mentorId,
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        if (instrument)
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.MemberInstruments.Add(MemberInstrument.Create(user.Id, InstrumentType.Guitarra, true));
            await db.SaveChangesAsync();
        }

        return user.Id;
    }
}
