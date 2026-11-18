// TreasuryApiTests tests: report create, publish, delete and totals.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class TreasuryApiTests
{
    [Fact]
    public async Task Reports_AreCreatedPublishedAndDeleted_WithTheOldRules()
    {
        var (admin, _) = await SignInAsync(null, "Admin");
        (await admin.PostAsJsonAsync("/api/treasury/reports", new { year = 2000 })).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "every write needs the antiforgery token");
        await AreaHttp.WithTokenAsync(admin);

        var years = (await AreaHttp.Json(admin, "/api/treasury")).GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()).ToList();
        years.Should().NotBeEmpty().And.BeInDescendingOrder().And.OnlyContain(y => y >= 1991);
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/treasury/reports", new { year = 1990 }))).Keys.Should().Equal("year");
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/treasury/reports", new { year = DateTime.Today.Year + 2 }))).Keys.Should().Equal("year");
        var year = years.Last();
        var created = await admin.PostAsJsonAsync("/api/treasury/reports", new { year });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = await created.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("title").GetString().Should().Be($"Relatório de Contas {year} - {year + 1}");
        report.GetProperty("isPublished").GetBoolean().Should().BeFalse();
        var id = report.GetProperty("id").GetInt32();
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/treasury/reports", new { year })))["year"].Single().Should().StartWith("Já existe um relatório");
        (await AreaHttp.Json(admin, "/api/treasury")).GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()).Should().NotContain(year);

        var (owner, _) = await SignInAsync(null, "Owner");
        await AreaHttp.WithTokenAsync(owner);
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
        var page = await AreaHttp.Json(member, $"/api/treasury/reports/{reportId}");
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

        var card = (await AreaHttp.Json(member, "/api/treasury")).GetProperty("reports").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == reportId);
        (card.GetProperty("totalIncome").GetDecimal(), card.GetProperty("totalExpenses").GetDecimal(), card.GetProperty("balance").GetDecimal())
            .Should().Be((170.10m, 30.05m, 140.05m), "the card and the page agree");
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
}
