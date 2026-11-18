// TreasuryApiTests tests: calotes, MBWay and Nerba.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class TreasuryApiTests
{
    [Fact]
    public async Task Calotes_AreManagedByModAdminOwner_WithCommitments_AndNothingIsSent()
    {
        var fy = await AddFiscalYearAsync();
        var (_, debtor) = await SignInAsync(MemberCategory.Caloiro);
        var (_, leitao) = await SignInAsync(MemberCategory.Leitao);
        var (owner, _) = await SignInAsync(null, "Owner");
        await WithTokenAsync(owner);
        _factory.Push.Invocations.Clear();

        (await Errors(await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = "1066-1067", userId = debtor.Id, amount = 5 }))).Keys.Should().Equal("fiscalYear");
        (await Errors(await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = leitao.Id, amount = 5 }))).Keys.Should().Equal(new[] { "userId" },
            "the old picker never offered Leitões");
        (await Errors(await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = debtor.Id, amount = 0 }))).Keys.Should().Equal("amount");
        (await Errors(await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = debtor.Id, amount = 5, description = new string('d', 501) })))
            .Keys.Should().Equal("description");

        var calotes = await Json(await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = debtor.Id, amount = 5, description = " Capa " }));
        calotes.GetProperty("fiscalYear").GetString().Should().Be(Label(fy));
        calotes.GetProperty("canManage").GetBoolean().Should().BeTrue();
        var group = Group(calotes, debtor.Id);
        group.GetProperty("debts").EnumerateArray().Single().GetProperty("description").GetString().Should().Be("Capa");

        calotes = await Json(await owner.PutAsJsonAsync("/api/treasury/calotes/commitment", new { fiscalYear = Label(fy), userId = debtor.Id, until = "2099-06-30" }));
        Group(calotes, debtor.Id).GetProperty("compromisedUntil").GetString().Should().StartWith("2099-06-30");
        calotes = await Json(await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = debtor.Id, amount = 2.5 }));
        group = Group(calotes, debtor.Id);
        group.GetProperty("total").GetDecimal().Should().Be(7.5m);
        await using (var db = await Db())
        {
            (await db.MemberDebts.Where(d => d.UserId == debtor.Id).Select(d => d.CompromisedUntil).ToListAsync())
                .Should().OnlyContain(d => d == new DateTime(2099, 6, 30), "a new debt joins the member's commitment, as before");
        }

        var debtId = group.GetProperty("debts").EnumerateArray().First().GetProperty("id").GetInt32();
        calotes = await Json(await owner.PutAsJsonAsync($"/api/treasury/calotes/{debtId}", new { amount = 6, description = "Capa nova" }));
        (await DebtAsync(debtId))!.CompromisedUntil.Should().Be(new DateTime(2099, 6, 30), "editing keeps the commitment");
        Group(calotes, debtor.Id).GetProperty("total").GetDecimal().Should().Be(8.5m);
        await Json(await owner.PutAsJsonAsync("/api/treasury/calotes/commitment", new { fiscalYear = Label(fy), userId = debtor.Id, until = (string?)null }));
        (await DebtAsync(debtId))!.CompromisedUntil.Should().BeNull();

        calotes = await Json(await owner.DeleteAsync($"/api/treasury/calotes/{debtId}"));
        Group(calotes, debtor.Id).GetProperty("debts").GetArrayLength().Should().Be(1);

        var (mod, _) = await SignInAsync(MemberCategory.Tuno, "Mod");
        await WithTokenAsync(mod);
        var picks = await mod.GetStringAsync($"/api/treasury/members?q={Uri.EscapeDataString(leitao.Email!)}&forDebts=true");
        picks.Should().Be("[]", "Leitões are not offered for debts");
        picks = await mod.GetStringAsync($"/api/treasury/members?q={Uri.EscapeDataString(debtor.Email!)}&forDebts=true");
        picks.Should().Contain(debtor.Id).And.NotContain(debtor.Email!, "e-mail is searched, never returned");

        _factory.Push.Invocations.Should().BeEmpty("calote changes send nothing");
        _factory.Email.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Mbway_ModAdds_OnlyOwnerEditsAndDeletes()
    {
        var (_, member) = await SignInAsync(MemberCategory.Tuno);
        var (mod, _) = await SignInAsync(MemberCategory.Tuno, "Mod");
        await WithTokenAsync(mod);

        (await Errors(await mod.PostAsJsonAsync("/api/treasury/mbway", new { date = "2026-03-01", amount = 5 }))).Keys.Should().Equal(new[] { "memberUserId" },
            "the old service refused a transfer without a member");
        (await Errors(await mod.PostAsJsonAsync("/api/treasury/mbway", new { date = "2026-03-01", amount = -1, memberUserId = member.Id }))).Keys.Should().Equal("amount");
        (await Errors(await mod.PostAsJsonAsync("/api/treasury/mbway", new { date = "2026-03-01", amount = 5, memberUserId = member.Id, phone = new string('9', 21) })))
            .Keys.Should().Equal("phone");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var transfers = await Json(await mod.PostAsJsonAsync("/api/treasury/mbway",
            new { date = "2026-03-01", amount = 12.5, memberUserId = member.Id, transferFrom = "Pai", phone = "912000000", description = tag }));
        transfers.GetProperty("canEdit").GetBoolean().Should().BeFalse();
        var added = transfers.GetProperty("transfers").EnumerateArray().Single(t => t.GetProperty("description").GetString() == tag);
        added.GetProperty("transferTo").GetString().Should().Be(member.Nickname, "the recipient is the member's nickname, as the old picker set it");
        added.GetProperty("member").ValueKind.Should().Be(JsonValueKind.Null);
        added.GetProperty("createdBy").GetString().Should().NotBeNullOrEmpty();
        transfers.GetProperty("totals").EnumerateArray().Should().Contain(t => t.GetProperty("name").GetString() == member.Nickname);
        var id = added.GetProperty("id").GetInt32();
        (await mod.PutAsJsonAsync($"/api/treasury/mbway/{id}", new { date = "2026-03-01", amount = 1, memberUserId = member.Id })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.DeleteAsync($"/api/treasury/mbway/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (owner, _) = await SignInAsync(null, "Owner");
        await WithTokenAsync(owner);
        var asOwner = (await Json(owner, "/api/treasury/mbway")).GetProperty("transfers").EnumerateArray().Single(t => t.GetProperty("id").GetInt32() == id);
        asOwner.GetProperty("member").GetProperty("id").GetString().Should().Be(member.Id, "the Owner edits, so the Owner gets the id");
        var edited = (await Json(await owner.PutAsJsonAsync($"/api/treasury/mbway/{id}", new { date = "2026-03-02", amount = 13, memberUserId = member.Id, description = tag })))
            .GetProperty("transfers").EnumerateArray().Single(t => t.GetProperty("id").GetInt32() == id);
        edited.GetProperty("amount").GetDecimal().Should().Be(13m);
        edited.GetProperty("transferFrom").ValueKind.Should().Be(JsonValueKind.Null);
        (await owner.DeleteAsync($"/api/treasury/mbway/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.DeleteAsync($"/api/treasury/mbway/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Nerba_OrdersPerDay_AreManagedByModAdminOwner()
    {
        var (eventId, _) = await AddNerbaAsync(days: 3);
        var (admin, _) = await SignInAsync(null, "Admin");
        await WithTokenAsync(admin);

        var orders = await Json(admin, $"/api/treasury/nerba/{eventId}");
        var days = orders.GetProperty("days").EnumerateArray().Select(d => d.GetString()![..10]).ToList();
        days.Should().HaveCount(3);
        (await Errors(await admin.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = " ", stock = 1, pricePerUnit = 1 }))).Keys.Should().Equal("item");
        (await Errors(await admin.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = "x", stock = 0, pricePerUnit = 1 }))).Keys.Should().Equal("stock");
        (await Errors(await admin.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = "x", stock = 1, pricePerUnit = -1 }))).Keys.Should().Equal("pricePerUnit");
        (await Errors(await admin.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = "x", stock = 1, pricePerUnit = 1, orderDate = "1999-01-01" })))
            .Keys.Should().Equal("orderDate");

        orders = await Json(await admin.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = "Copos", type = "Plástico", stock = 3, pricePerUnit = 1.25 }));
        var copos = orders.GetProperty("orders").EnumerateArray().Single(o => o.GetProperty("item").GetString() == "Copos");
        copos.GetProperty("total").GetDecimal().Should().Be(3.75m);
        copos.GetProperty("orderDate").GetString()![..10].Should().Be(days[0], "the first day by default, as the old form");
        var id = copos.GetProperty("id").GetInt32();
        orders = await Json(await admin.PutAsJsonAsync($"/api/treasury/nerba/orders/{id}", new { item = "Copos", stock = 4, pricePerUnit = 1.25, orderDate = days[2] }));
        copos = orders.GetProperty("orders").EnumerateArray().Single(o => o.GetProperty("id").GetInt32() == id);
        (copos.GetProperty("total").GetDecimal(), copos.GetProperty("orderDate").GetString()![..10]).Should().Be((5m, days[2]));
        copos.GetProperty("type").ValueKind.Should().Be(JsonValueKind.Null);

        var summary = (await Json(admin, "/api/treasury/nerba")).GetProperty("upcoming").EnumerateArray().Single(s => s.GetProperty("event").GetProperty("id").GetInt32() == eventId);
        (summary.GetProperty("count").GetInt32(), summary.GetProperty("total").GetDecimal()).Should().Be((2, 8m), "Pratos 2 × 1,50 + Copos 4 × 1,25");

        var other = await AddEventAsync(EventType.Atuacao);
        (await admin.GetAsync($"/api/treasury/nerba/{other}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "only Nerba events have orders here");
        (await admin.PostAsJsonAsync($"/api/treasury/nerba/{other}/orders", new { item = "x", stock = 1, pricePerUnit = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Json(await admin.DeleteAsync($"/api/treasury/nerba/orders/{id}"))).GetProperty("orders").GetArrayLength().Should().Be(1);
        var nerba = await Json(await admin.DeleteAsync($"/api/treasury/nerba/{eventId}"));
        nerba.GetProperty("upcoming").EnumerateArray().Single(s => s.GetProperty("event").GetProperty("id").GetInt32() == eventId)
            .GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Owner_InheritsAdmin_AndMbwayCalotesNerbaWritesNeedAntiforgery()
    {
        var (owner, _) = await SignInAsync(null, "Owner");
        var (_, member) = await SignInAsync(MemberCategory.Tuno);
        var fy = await AddFiscalYearAsync();
        var (eventId, _) = await AddNerbaAsync();
        (await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = member.Id, amount = 1 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsJsonAsync("/api/treasury/mbway", new { date = "2026-01-01", amount = 1, memberUserId = member.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = "x", stock = 1, pricePerUnit = 1 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DebtsOfAsync(member.Id)).Should().BeEmpty("nothing was written without the token");

        await WithTokenAsync(owner);
        (await owner.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = member.Id, amount = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsJsonAsync("/api/treasury/mbway", new { date = "2026-01-01", amount = 1, memberUserId = member.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsJsonAsync($"/api/treasury/nerba/{eventId}/orders", new { item = "x", stock = 1, pricePerUnit = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static JsonElement Group(JsonElement calotes, string userId) =>
        calotes.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("member").GetProperty("id").GetString() == userId);

    private async Task<List<MemberDebt>> DebtsOfAsync(string userId)
    {
        await using var db = await Db();
        return await db.MemberDebts.AsNoTracking().Where(d => d.UserId == userId).ToListAsync();
    }
}
