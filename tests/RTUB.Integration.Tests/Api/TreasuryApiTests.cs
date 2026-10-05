using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Tesouraria on /api/treasury (React track 024; were the Blazor /finance, /finance/report/{id}, /calotes, /mbway and
/// /nerba pages) through the real host: real login, roles, antiforgery and SQLite. Receipts go to
/// <see cref="FakeReceiptStorage"/> (in memory, nothing reaches R2); push and e-mail are recording mocks and the daily calote
/// reminder is switched off, so nothing is ever sent. Every test makes its own reports, fiscal years and events, since the
/// class shares one database.
/// </summary>
public class TreasuryApiTests : IClassFixture<TreasuryApiFactory>
{
    private readonly TreasuryApiFactory _factory;

    public TreasuryApiTests(TreasuryApiFactory factory)
    {
        _factory = factory;
    }

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

        var old = new Dictionary<string, string>
        {
            ["/finance"] = "/treasury",
            [$"/finance/report/{reportId}"] = $"/treasury/reports/{reportId}",
            ["/calotes"] = "/treasury/calotes",
            ["/calotes?fy=2025-2026&reportId=3"] = "/treasury/calotes?fy=2025-2026",
            ["/mbway"] = "/treasury/mbway",
            ["/mbway/3"] = "/treasury/mbway",
            ["/nerba"] = "/treasury/nerba",
            ["/nerba/3"] = "/treasury/nerba",
            ["/nerba/event/7"] = "/treasury/nerba/7",
        };
        foreach (var (from, to) in old)
        {
            var response = await anonymous.GetAsync(from);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, from);
            response.Headers.Location!.ToString().Should().Be(to, from);
        }

        var (member, _) = await SignInAsync(MemberCategory.Tuno);
        foreach (var page in new[] { "/treasury", $"/treasury/reports/{reportId}", "/treasury/calotes", "/treasury/mbway", "/treasury/nerba", "/treasury/nerba/1" })
        {
            (await member.GetStringAsync(page)).Should().Contain("id=\"root\"", page).And.NotContain("blazor.web.js", page);
        }

        var web = typeof(RTUB.App).Assembly;
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

    [Fact]
    public async Task Reports_AreCreatedPublishedAndDeleted_WithTheOldRules()
    {
        var (admin, _) = await SignInAsync(null, "Admin");
        (await admin.PostAsJsonAsync("/api/treasury/reports", new { year = 2000 })).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "every write needs the antiforgery token");
        await WithTokenAsync(admin);

        var years = (await Json(admin, "/api/treasury")).GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()).ToList();
        years.Should().NotBeEmpty().And.BeInDescendingOrder().And.OnlyContain(y => y >= 1991);
        (await Errors(await admin.PostAsJsonAsync("/api/treasury/reports", new { year = 1990 }))).Keys.Should().Equal("year");
        (await Errors(await admin.PostAsJsonAsync("/api/treasury/reports", new { year = DateTime.Today.Year + 2 }))).Keys.Should().Equal("year");
        var year = years.Last();
        var created = await admin.PostAsJsonAsync("/api/treasury/reports", new { year });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = await created.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("title").GetString().Should().Be($"Relatório de Contas {year} - {year + 1}");
        report.GetProperty("isPublished").GetBoolean().Should().BeFalse();
        var id = report.GetProperty("id").GetInt32();
        (await Errors(await admin.PostAsJsonAsync("/api/treasury/reports", new { year })))["year"].Single().Should().StartWith("Já existe um relatório");
        (await Json(admin, "/api/treasury")).GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()).Should().NotContain(year);

        var (owner, _) = await SignInAsync(null, "Owner");
        await WithTokenAsync(owner);
        await AddReceiptTransactionAsync(id);
        var receipts = _factory.Receipts.Objects.Count;
        (await owner.DeleteAsync($"/api/treasury/reports/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReportAsync(id)).Should().BeNull();
        _factory.Receipts.Objects.Count.Should().Be(receipts - 1, "a deleted draft takes its receipts with it");

        var (published, _, _) = await SeedReportAsync();
        (await owner.PostAsync($"/api/treasury/reports/{published}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsync($"/api/treasury/reports/{published}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.DeleteAsync($"/api/treasury/reports/{published}")).StatusCode.Should().Be(HttpStatusCode.Conflict, "only drafts are deleted");
    }

    [Fact]
    public async Task ReportTotals_MatchTheOldPage()
    {
        var year = Unique(1700);
        int reportId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var report = Report.Create($"Totais {year}", year);
            db.Reports.Add(report);
            var fy = FiscalYear.Create(year, year + 1);
            db.FiscalYears.Add(fy);
            await db.SaveChangesAsync();
            reportId = report.Id;

            void Add(string name, DateTime start, params (decimal Amount, string Type)[] tx)
            {
                var a = Activity.Create(report.Id, name, start);
                foreach (var (amount, type) in tx)
                {
                    a.Transactions.Add(Transaction.Create(start, name, "Teste", amount, type));
                }

                db.Activities.Add(a);
            }

            Add("Jantar", new DateTime(year, 11, 2), (100.10m, "Income"), (30.05m, "Expense"));
            Add("Festival", new DateTime(year, 10, 1), (50m, "Income"));
            Add("DINHEIRO NO BANCO", new DateTime(year, 9, 1), (500m, "Income"));
            Add("DINHEIRO EM CAIXA", new DateTime(year, 9, 1), (20m, "Income"));
            Add("Calotes", new DateTime(year, 9, 1), (999m, "Income"));
            await db.SaveChangesAsync();
            db.MemberDebts.Add(MemberDebt.Create((await db.Users.FirstAsync()).Id, 7.5m, null, fy.Id));
            await db.SaveChangesAsync();
        }

        var (member, _) = await SignInAsync(MemberCategory.Tuno);
        var page = await Json(member, $"/api/treasury/reports/{reportId}");
        var totals = page.GetProperty("totals");
        totals.GetProperty("bank").GetDecimal().Should().Be(500m);
        totals.GetProperty("cash").GetDecimal().Should().Be(20m);
        totals.GetProperty("totalMoney").GetDecimal().Should().Be(520m);
        totals.GetProperty("calotes").GetDecimal().Should().Be(7.5m);
        totals.GetProperty("income").GetDecimal().Should().Be(170.10m, "BANCO and CALOTES are left out, CAIXA is counted (Report.TotalIncome)");
        totals.GetProperty("expenses").GetDecimal().Should().Be(30.05m);
        totals.GetProperty("balance").GetDecimal().Should().Be(140.05m);
        page.GetProperty("activities").EnumerateArray().Select(a => a.GetProperty("name").GetString())
            .Should().Equal(new[] { "Festival", "Jantar" }, "BANCO, CAIXA and CALOTES are hidden; by start date");

        var card = (await Json(member, "/api/treasury")).GetProperty("reports").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == reportId);
        (card.GetProperty("totalIncome").GetDecimal(), card.GetProperty("totalExpenses").GetDecimal(), card.GetProperty("balance").GetDecimal())
            .Should().Be((170.10m, 30.05m, 140.05m), "the card and the page agree");
    }

    [Fact]
    public async Task ActivitiesTransactionsAndReceipts_KeepTheOldRules()
    {
        var (reportId, _, _) = await SeedReportAsync();
        var (manager, treasurer) = await SignInAsync(MemberCategory.Tuno, "Mod");
        await UpdateUserAsync(treasurer.Id, u => u.Positions = new List<Position> { Position.PrimeiroTesoureiro });
        await WithTokenAsync(manager);

        (await Errors(await manager.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities", new { name = " ", startDate = "2026-01-01" }))).Keys.Should().Equal("name");
        (await Errors(await manager.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities", new { name = "x" }))).Keys.Should().Equal("startDate");
        (await Errors(await manager.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities",
            new { name = "x", startDate = "2026-02-02", endDate = "2026-02-01" }))).Keys.Should().Equal("endDate");
        var report = await Json(await manager.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities",
            new { name = " Arraial ", startDate = "2026-02-01", endDate = "2026-02-03", description = "Barraca" }));
        var activity = report.GetProperty("activities").EnumerateArray().Single(a => a.GetProperty("name").GetString() == "Arraial");
        var activityId = activity.GetProperty("id").GetInt32();
        activity.GetProperty("endDate").GetString().Should().StartWith("2026-02-03");

        (await Errors(await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields(amount: "0"))))).Keys.Should().Equal("amount");
        (await Errors(await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields(amount: "1.005"))))).Keys.Should().Equal("amount");
        (await Errors(await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields(type: "Gift"))))).Keys.Should().Equal("type");
        (await Errors(await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields(description: " "))))).Keys.Should().Equal("description");
        (await Errors(await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields(), ("recibo.exe", "application/x-msdownload", 10)))))
            .Keys.Should().Equal("receipt");
        _factory.Receipts.Objects.Keys.Should().NotContain(k => k.Contains("recibo"), "nothing is uploaded for a refused form");

        report = await Json(await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions",
            Form(Fields(amount: "12.34", type: "Income", description: "Bilhetes"), ("recibo.pdf", "application/pdf", 64))));
        var tx = ActivityOf(report, activityId).GetProperty("transactions").EnumerateArray().Single();
        var txId = tx.GetProperty("id").GetInt32();
        tx.GetProperty("amount").GetDecimal().Should().Be(12.34m);
        var receipt = tx.GetProperty("receiptUrl").GetString()!;
        _factory.Receipts.Objects.Should().ContainKey(receipt);
        ActivityOf(report, activityId).GetProperty("balance").GetDecimal().Should().Be(12.34m);

        report = await Json(await manager.PutAsync($"/api/treasury/transactions/{txId}",
            Form(Fields(amount: "20", type: "Expense", description: "Cordas"), ("novo.png", "image/png", 32))));
        var replaced = ActivityOf(report, activityId).GetProperty("transactions").EnumerateArray().Single().GetProperty("receiptUrl").GetString()!;
        replaced.Should().NotBe(receipt);
        _factory.Receipts.Objects.Should().NotContainKey(receipt, "a replaced receipt is deleted, as before").And.ContainKey(replaced);
        report = await Json(await manager.PutAsync($"/api/treasury/transactions/{txId}", Form(Fields(amount: "20", description: "Cordas", removeReceipt: true))));
        ActivityOf(report, activityId).GetProperty("transactions").EnumerateArray().Single().GetProperty("receiptUrl").ValueKind.Should().Be(JsonValueKind.Null);
        _factory.Receipts.Objects.Should().NotContainKey(replaced);

        // A locked activity takes no new transaction; its transactions can still change (as before).
        ActivityOf(await Json(await manager.PostAsJsonAsync($"/api/treasury/activities/{activityId}/lock", new { locked = true })), activityId)
            .GetProperty("isLocked").GetBoolean().Should().BeTrue();
        (await manager.PostAsync($"/api/treasury/activities/{activityId}/transactions", Form(Fields()))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.PutAsync($"/api/treasury/transactions/{txId}", Form(Fields(amount: "21", description: "Cordas")))).StatusCode.Should().Be(HttpStatusCode.OK);
        await Json(await manager.PostAsJsonAsync($"/api/treasury/activities/{activityId}/lock", new { locked = false }));

        // Bank / cash: one "Saldo" transaction in DINHEIRO NO BANCO; negative becomes an expense; the activity stays hidden.
        (await Errors(await manager.PutAsJsonAsync($"/api/treasury/reports/{reportId}/balance", new { kind = "safe", value = 1 }))).Keys.Should().Equal("kind");
        var totals = (await Json(await manager.PutAsJsonAsync($"/api/treasury/reports/{reportId}/balance", new { kind = "bank", value = -15.5 }))).GetProperty("totals");
        totals.GetProperty("bank").GetDecimal().Should().Be(-15.5m);
        totals = (await Json(await manager.PutAsJsonAsync($"/api/treasury/reports/{reportId}/balance", new { kind = "bank", value = 300 }))).GetProperty("totals");
        totals.GetProperty("bank").GetDecimal().Should().Be(300m);
        await using (var db = await Db())
        {
            var bank = await db.Activities.Include(a => a.Transactions).SingleAsync(a => a.ReportId == reportId && a.Name == "DINHEIRO NO BANCO");
            bank.Transactions.Should().ContainSingle().Which.Category.Should().Be("Saldo");
        }

        // A published report is read-only, but activities can still be locked / unlocked (as before).
        var (owner, _) = await SignInAsync(null, "Owner");
        await WithTokenAsync(owner);
        await Json(await owner.PostAsync($"/api/treasury/reports/{reportId}/publish", null));
        (await manager.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities", new { name = "x", startDate = "2026-01-01" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.PutAsync($"/api/treasury/transactions/{txId}", Form(Fields()))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.DeleteAsync($"/api/treasury/transactions/{txId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.DeleteAsync($"/api/treasury/activities/{activityId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.PutAsJsonAsync($"/api/treasury/reports/{reportId}/balance", new { kind = "cash", value = 1 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.PostAsJsonAsync($"/api/treasury/activities/{activityId}/lock", new { locked = true })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletingAnActivity_TakesItsTransactionsAndReceipts()
    {
        var (reportId, activityId, transactionId) = await SeedReportAsync(receipt: true);
        var receipt = (await TransactionAsync(transactionId))!.ReceiptUrl!;
        _factory.Receipts.Objects.Should().ContainKey(receipt);
        var (admin, _) = await SignInAsync(null, "Admin");
        await WithTokenAsync(admin);

        var report = await Json(await admin.DeleteAsync($"/api/treasury/activities/{activityId}"));
        report.GetProperty("activities").EnumerateArray().Should().NotContain(a => a.GetProperty("id").GetInt32() == activityId);
        (await TransactionAsync(transactionId)).Should().BeNull();
        _factory.Receipts.Objects.Should().NotContainKey(receipt, "its transactions go first, receipts included (the old delete failed on the foreign key)");
        (await admin.DeleteAsync($"/api/treasury/activities/{activityId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        _ = reportId;
    }

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

    // ---------------------------------------------------------------- helpers

    private static int _unique;

    private static int Unique(int floor) => floor + Interlocked.Increment(ref _unique);

    private static string Label(int startYear) => $"{startYear}-{startYear + 1}";

    private static JsonElement ActivityOf(JsonElement report, int id) =>
        report.GetProperty("activities").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == id);

    private static JsonElement Group(JsonElement calotes, string userId) =>
        calotes.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("member").GetProperty("id").GetString() == userId);

    private static Dictionary<string, string> Fields(string amount = "10", string type = "Expense", string description = "Cordas", bool removeReceipt = false) => new()
    {
        ["date"] = "2026-02-02",
        ["description"] = description,
        ["category"] = "Material",
        ["amount"] = amount,
        ["type"] = type,
        ["removeReceipt"] = removeReceipt ? "true" : "false",
    };

    private static MultipartFormDataContent Form(Dictionary<string, string> fields, (string Name, string Type, int Size)? file = null)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            form.Add(new StringContent(value), key);
        }

        if (file is { } f)
        {
            var content = new ByteArrayContent(new byte[f.Size]);
            content.Headers.ContentType = new MediaTypeHeaderValue(f.Type);
            form.Add(content, "receipt", f.Name);
        }

        return form;
    }

    private async Task<ApplicationDbContext> Db() =>
        await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    /// <summary>A draft report with one activity and one transaction (optionally with a receipt in the fake bucket).</summary>
    private async Task<(int ReportId, int ActivityId, int TransactionId)> SeedReportAsync(bool receipt = false)
    {
        await using var db = await Db();
        var report = Report.Create("Relatório de teste", Unique(1500));
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        var activity = Activity.Create(report.Id, "Serenata", new DateTime(2026, 1, 10));
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        var url = receipt ? _factory.Receipts.Put($"receipts/test/seed_{Guid.NewGuid():N}.pdf") : null;
        var transaction = Transaction.Create(new DateTime(2026, 1, 10), "Jantar", "Comida", 25m, "Expense", activity.Id, url);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return (report.Id, activity.Id, transaction.Id);
    }

    private async Task AddReceiptTransactionAsync(int reportId)
    {
        await using var db = await Db();
        var activity = Activity.Create(reportId, "Com recibo", new DateTime(2026, 1, 10));
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        db.Transactions.Add(Transaction.Create(DateTime.Today, "x", "y", 1m, "Income", activity.Id, _factory.Receipts.Put($"receipts/test/r_{Guid.NewGuid():N}.png")));
        await db.SaveChangesAsync();
    }

    private async Task<int> AddFiscalYearAsync()
    {
        await using var db = await Db();
        var start = Unique(1300);
        var year = FiscalYear.Create(start, start + 1);
        db.FiscalYears.Add(year);
        await db.SaveChangesAsync();
        return year.StartYear;
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

    private async Task<int> AddEventAsync(EventType type, int days = 1)
    {
        await using var db = await Db();
        var e = Event.Create($"Evento {Guid.NewGuid():N}"[..20], DateTime.Today.AddDays(30), "Bragança", type, "");
        if (days > 1)
        {
            e.EndDate = e.Date.AddDays(days - 1);
        }

        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<(int EventId, int OrderId)> AddNerbaAsync(int days = 1)
    {
        var eventId = await AddEventAsync(EventType.Nerba, days);
        await using var db = await Db();
        var order = new NerbaOrder { EventId = eventId, Item = "Pratos", Stock = 2, PricePerUnit = 1.5m };
        db.NerbaOrders.Add(order);
        await db.SaveChangesAsync();
        return (eventId, order.Id);
    }

    private async Task<Report?> ReportAsync(int id)
    {
        await using var db = await Db();
        return await db.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
    }

    private async Task<Transaction?> TransactionAsync(int id)
    {
        await using var db = await Db();
        return await db.Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
    }

    private async Task<MemberDebt?> DebtAsync(int id)
    {
        await using var db = await Db();
        return await db.MemberDebts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
    }

    private async Task<List<MemberDebt>> DebtsOfAsync(string userId)
    {
        await using var db = await Db();
        return await db.MemberDebts.AsNoTracking().Where(d => d.UserId == userId).ToListAsync();
    }

    private async Task UpdateUserAsync(string id, Action<ApplicationUser> change)
    {
        await using var db = await Db();
        change(await db.Users.SingleAsync(u => u.Id == id));
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static int _ip;

    /// <summary>A signed-in account with one category (or none) and an optional role.</summary>
    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(MemberCategory? category, string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"trs{Guid.NewGuid():N}"[..20], $"10.95.{n / 250}.{n % 250 + 1}", role);
        await UpdateUserAsync(user.Id, u => u.Categories = category is { } c ? new List<MemberCategory> { c } : new List<MemberCategory>());
        user.Categories = category is { } cat ? new List<MemberCategory> { cat } : new List<MemberCategory>();
        return (client, user);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.96.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}

/// <summary>The test host for Tesouraria: receipts in memory, push and e-mail recorded, the daily calote reminder off.</summary>
public sealed class TreasuryApiFactory : TestWebApplicationFactory
{
    public FakeReceiptStorage Receipts { get; } = new();
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["CalotesNotifications:Enabled"] = "false" }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReceiptStorageService>();
            services.AddSingleton<IReceiptStorageService>(Receipts);
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<IEmailNotificationService>();
            services.AddSingleton(Email.Object);
        });
    }
}

/// <summary>An in-memory receipt bucket: URLs are fake public URLs; nothing reaches R2.</summary>
public sealed class FakeReceiptStorage : IReceiptStorageService
{
    private const string Base = "https://receipts.test/";

    public ConcurrentDictionary<string, long> Objects { get; } = new();

    public string Put(string key)
    {
        Objects[Base + key] = 1;
        return Base + key;
    }

    public async Task<string> UploadReceiptAsync(Stream fileStream, string fileName, string contentType, int transactionId)
    {
        using var copy = new MemoryStream();
        await fileStream.CopyToAsync(copy);
        var url = $"{Base}receipts/test/{transactionId}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        Objects[url] = copy.Length;
        return url;
    }

    public Task DeleteReceiptAsync(string receiptUrl)
    {
        Objects.TryRemove(receiptUrl, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ReceiptExistsAsync(string receiptUrl) => Task.FromResult(Objects.ContainsKey(receiptUrl));
}
