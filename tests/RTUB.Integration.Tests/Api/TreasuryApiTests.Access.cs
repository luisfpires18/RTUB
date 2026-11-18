// TreasuryApiTests tests: who may read and manage the treasury.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class TreasuryApiTests
{
    [Fact]
    public async Task Visitors_GetNothing_PagesAreReact_AndOldRoutesRedirect()
    {
        var (reportId, activityId, transactionId) = await SeedReportAsync();
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/treasury", $"/api/treasury/reports/{reportId}", $"/api/treasury/reports/{reportId}/pdf",
                     $"/api/treasury/reports/{reportId}/history", "/api/treasury/calotes", "/api/treasury/mbway", "/api/treasury/nerba",
                     "/api/treasury/nerba/1", "/api/treasury/members?q=a" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync("/api/treasury/reports", new { year = 2000 })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/treasury/calotes", new { amount = 1 })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields()))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/treasury/transactions/{transactionId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        foreach (var page in new[] { "/treasury", $"/treasury/reports/{reportId}", "/treasury/calotes", "/treasury/mbway", "/treasury/nerba", "/treasury/nerba/1" })
        {
            var response = await anonymous.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, page);
            response.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(page));
        }

        var (member, _) = await SignInAsync(MemberCategory.Tuno);
        foreach (var page in new[] { "/treasury", $"/treasury/reports/{reportId}", "/treasury/calotes", "/treasury/mbway", "/treasury/nerba", "/treasury/nerba/1" })
        {
            (await member.GetStringAsync(page)).Should().Contain("id=\"root\"", page).And.NotContain("blazor.web.js", page);
        }

        var web = typeof(RTUB.Program).Assembly;
        foreach (var retired in new[] { "Finance", "Report", "MbwayTransfers", "Nerba", "NerbaOrderDetail", "Calotes" })
        {
            web.GetTypes().Where(t => t.Name == retired && t.IsAssignableTo(typeof(ComponentBase))).Should().BeEmpty($"the Blazor {retired} page was retired");
        }

        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/finance", "/finance/report/{ReportId:int}", "/calotes", "/mbway", "/mbway/{ReportId:int}", "/nerba",
                "/nerba/{ReportId:int}", "/nerba/event/{EventId:int}", "/logistics", "/members/manage", "/member/events", "/member/gallery",
                "/member/roles", "/hierarchy" });
    }

    [Fact]
    public async Task CaloirosAndLeitoes_AreRefusedTheTreasury_ButSeeTheirOwnCalotesOnly()
    {
        var (reportId, _, _) = await SeedReportAsync();
        var fy = await AddFiscalYearAsync();
        var (_, other) = await SignInAsync(MemberCategory.Tuno);
        await AddDebtAsync(other.Id, fy, 40m, "Jantar de outro");

        foreach (var category in new[] { MemberCategory.Caloiro, MemberCategory.Leitao })
        {
            var (client, me) = await SignInAsync(category);
            await AddDebtAsync(me.Id, fy, 12.5m, "Capa");
            await AddDebtAsync(me.Id, fy, 2.25m, null);
            await WithTokenAsync(client);
            foreach (var path in new[] { "/api/treasury", $"/api/treasury/reports/{reportId}", $"/api/treasury/reports/{reportId}/pdf",
                         "/api/treasury/mbway", "/api/treasury/nerba", "/api/treasury/members?q=a" })
            {
                (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{category} {path}");
            }

            var raw = await client.GetStringAsync($"/api/treasury/calotes?fy={Label(fy)}");
            raw.Should().NotContain(other.Id).And.NotContain(other.UserName!).And.NotContain("Jantar de outro", "only their own debts");
            raw.Should().NotContain(me.Id, "no member id leaves the server for a non-manager").And.NotContain(me.Email!);
            var calotes = JsonDocument.Parse(raw).RootElement;
            calotes.GetProperty("scope").GetString().Should().Be("own");
            calotes.GetProperty("canManage").GetBoolean().Should().BeFalse();
            calotes.GetProperty("total").GetDecimal().Should().Be(14.75m, "the total counts their own debts only");
            var group = calotes.GetProperty("groups").EnumerateArray().Single();
            group.GetProperty("member").GetProperty("displayName").GetString().Should().Be(me.Nickname);
            group.GetProperty("debts").GetArrayLength().Should().Be(2);

            (await client.PostAsJsonAsync("/api/treasury/calotes", new { fiscalYear = Label(fy), userId = me.Id, amount = 1 }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Members_ReadEverything_WithoutPrivateFields_ButManageNothing()
    {
        var (reportId, activityId, transactionId) = await SeedReportAsync(receipt: true);
        var fy = await AddFiscalYearAsync();
        var (_, debtor) = await SignInAsync(MemberCategory.Tuno);
        var debtId = await AddDebtAsync(debtor.Id, fy, 9m, "Quota");
        var transferId = await AddTransferAsync(debtor.Id, "Nota interna", "912345678");
        var (eventId, orderId) = await AddNerbaAsync();
        var (member, _) = await SignInAsync(MemberCategory.Tuno);
        await WithTokenAsync(member);

        var reports = await Json(member, "/api/treasury");
        (reports.GetProperty("canCreate").GetBoolean(), reports.GetProperty("canPublish").GetBoolean()).Should().Be((false, false));
        reports.GetProperty("availableYears").GetArrayLength().Should().Be(0, "only managers get the create form's years");
        var report = await Json(member, $"/api/treasury/reports/{reportId}");
        (report.GetProperty("canManage").GetBoolean(), report.GetProperty("canSeeHistory").GetBoolean()).Should().Be((false, false));
        report.GetProperty("activities").EnumerateArray().SelectMany(a => a.GetProperty("transactions").EnumerateArray())
            .Single(t => t.GetProperty("id").GetInt32() == transactionId).GetProperty("receiptUrl").GetString().Should().StartWith("https://receipts.test/");
        var pdf = await member.GetAsync($"/api/treasury/reports/{reportId}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var calotes = await member.GetStringAsync($"/api/treasury/calotes?fy={Label(fy)}");
        calotes.Should().Contain("Quota").And.NotContain(debtor.Id).And.NotContain(debtor.Email!);
        JsonDocument.Parse(calotes).RootElement.GetProperty("scope").GetString().Should().Be("all");

        var transfers = await member.GetStringAsync("/api/treasury/mbway");
        transfers.Should().NotContain(debtor.Id, "only the Owner, who edits, gets the member id").And.NotContain(debtor.Email!);
        var t = JsonDocument.Parse(transfers).RootElement;
        (t.GetProperty("canAdd").GetBoolean(), t.GetProperty("canEdit").GetBoolean()).Should().Be((false, false));

        (await Json(member, $"/api/treasury/nerba/{eventId}")).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await member.GetAsync("/api/treasury/members?q=a")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync($"/api/treasury/reports/{reportId}/history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var refused = new (string Method, string Path, object? Body)[]
        {
            ("POST", "/api/treasury/reports", new { year = 1999 }),
            ("POST", $"/api/treasury/reports/{reportId}/publish", null),
            ("DELETE", $"/api/treasury/reports/{reportId}", null),
            ("PUT", $"/api/treasury/reports/{reportId}/balance", new { kind = "bank", value = 10 }),
            ("POST", $"/api/treasury/reports/{reportId}/activities", new { name = "x", startDate = "2026-01-01" }),
            ("PUT", $"/api/treasury/activities/{activityId}", new { name = "x", startDate = "2026-01-01" }),
            ("POST", $"/api/treasury/activities/{activityId}/lock", new { locked = true }),
            ("DELETE", $"/api/treasury/activities/{activityId}", null),
            ("DELETE", $"/api/treasury/transactions/{transactionId}", null),
            ("POST", "/api/treasury/calotes", new { fiscalYear = Label(fy), userId = debtor.Id, amount = 1 }),
            ("PUT", $"/api/treasury/calotes/{debtId}", new { amount = 1 }),
            ("DELETE", $"/api/treasury/calotes/{debtId}", null),
            ("PUT", "/api/treasury/calotes/commitment", new { fiscalYear = Label(fy), userId = debtor.Id, until = "2030-01-01" }),
            ("POST", "/api/treasury/mbway", new { date = "2026-01-01", amount = 1, memberUserId = debtor.Id }),
            ("PUT", $"/api/treasury/mbway/{transferId}", new { date = "2026-01-01", amount = 1, memberUserId = debtor.Id }),
            ("DELETE", $"/api/treasury/mbway/{transferId}", null),
            ("DELETE", $"/api/treasury/nerba/{eventId}", null),
            ("POST", $"/api/treasury/nerba/{eventId}/orders", new { item = "x", stock = 1, pricePerUnit = 1 }),
            ("PUT", $"/api/treasury/nerba/orders/{orderId}", new { item = "x", stock = 1, pricePerUnit = 1 }),
            ("DELETE", $"/api/treasury/nerba/orders/{orderId}", null),
        };
        foreach (var (method, path, body) in refused)
        {
            (await member.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = body is null ? null : JsonContent.Create(body) }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
        }

        (await member.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsync($"/api/treasury/transactions/{transactionId}", Form(Fields()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DebtAsync(debtId))!.AmountOwed.Should().Be(9m, "nothing changed");
        (await ReportAsync(reportId))!.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task ReportRights_FollowTheOldPages_AndOwnerInheritsAdmin()
    {
        var (reportId, _, _) = await SeedReportAsync();
        var (mod, _) = await SignInAsync(MemberCategory.Tuno, "Mod");
        var (treasurerMod, treasurer) = await SignInAsync(MemberCategory.Tuno, "Mod");
        await UpdateUserAsync(treasurer.Id, u => u.Positions = new List<Position> { Position.SegundoTesoureiro });
        var (treasurerMember, tm) = await SignInAsync(MemberCategory.Tuno);
        await UpdateUserAsync(tm.Id, u => u.Positions = new List<Position> { Position.PrimeiroTesoureiro });
        var (admin, _) = await SignInAsync(MemberCategory.Tuno, "Admin");
        var (owner, _) = await SignInAsync(null, "Owner");

        (bool Manage, bool Publish, bool History) Rights(JsonElement r) =>
            (r.GetProperty("canManage").GetBoolean(), r.GetProperty("canPublish").GetBoolean(), r.GetProperty("canSeeHistory").GetBoolean());

        Rights(await Json(mod, $"/api/treasury/reports/{reportId}")).Should().Be((false, false, false), "a Mod outside the treasury team only reads");
        Rights(await Json(treasurerMod, $"/api/treasury/reports/{reportId}")).Should().Be((true, true, false));
        Rights(await Json(treasurerMember, $"/api/treasury/reports/{reportId}")).Should().Be((false, true, false), "publishing was for the treasury team");
        Rights(await Json(admin, $"/api/treasury/reports/{reportId}")).Should().Be((true, false, true), "Admin never published");
        Rights(await Json(owner, $"/api/treasury/reports/{reportId}")).Should().Be((true, true, true), "Owner without Admin has every right");

        (await owner.GetAsync($"/api/treasury/reports/{reportId}/history")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await treasurerMod.GetAsync($"/api/treasury/reports/{reportId}/history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await WithTokenAsync(mod);
        (await mod.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities", new { name = "x", startDate = "2026-01-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await WithTokenAsync(admin);
        (await admin.PostAsync($"/api/treasury/reports/{reportId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await WithTokenAsync(treasurerMember);
        (await treasurerMember.PostAsync($"/api/treasury/reports/{reportId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReportAsync(reportId))!.IsPublished.Should().BeTrue();
    }

    private async Task<int> AddDebtAsync(string userId, int fiscalStartYear, decimal amount, string? description)
    {
        await using var db = await Db();
        var fy = await db.FiscalYears.SingleAsync(f => f.StartYear == fiscalStartYear);
        var debt = MemberDebt.Create(userId, amount, description, fy.Id);
        db.MemberDebts.Add(debt);
        await db.SaveChangesAsync();
        return debt.Id;
    }

    private async Task<int> AddTransferAsync(string memberId, string description, string phone)
    {
        await using var db = await Db();
        var t = new MbwayTransfer { Date = DateTime.Today, Amount = 3m, MemberUserId = memberId, TransferTo = "Alguém", Phone = phone, Description = description };
        db.MbwayTransfers.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.96.0.1");
        return client;
    }
}
