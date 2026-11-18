// TreasuryApiTests tests: activities, transactions, receipts and deletes.
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
    public async Task DeletingAReport_IsOneDatabaseTransaction_RolledBackWhole_WhenAnyRowFails()
    {
        var (reportId, activityId, transactionId) = await SeedReportAsync(receipt: true);
        var receipt = (await TransactionAsync(transactionId))!.ReceiptUrl!;
        int second;
        await using (var db = await Db())
        {
            var t = Transaction.Create(new DateTime(2026, 1, 11), "Bebidas", "Comida", 5m, "Expense", activityId);
            db.Transactions.Add(t);
            // NerbaOrders.ReportId is ON DELETE RESTRICT: the report row cannot go, after its transactions were already removed.
            db.NerbaOrders.Add(new NerbaOrder { EventId = (await AddNerbaAsync()).EventId, ReportId = reportId, Item = "Trava", Stock = 1, PricePerUnit = 1m });
            await db.SaveChangesAsync();
            second = t.Id;
        }

        var (owner, _) = await SignInAsync(null, "Owner");
        await WithTokenAsync(owner);
        (await owner.DeleteAsync($"/api/treasury/reports/{reportId}")).IsSuccessStatusCode.Should().BeFalse();

        (await ReportAsync(reportId)).Should().NotBeNull("the failed delete rolled back");
        (await TransactionAsync(transactionId)).Should().NotBeNull("no financial row is deleted unless all are");
        (await TransactionAsync(second)).Should().NotBeNull();
        _factory.Receipts.Objects.Should().ContainKey(receipt, "receipts go only after the rows are committed");
        await using (var db = await Db())
        {
            (await db.AuditLogs.AnyAsync(l => l.EntityType == "Transaction" && l.Action == "Deleted" && (l.EntityId == transactionId || l.EntityId == second)))
                .Should().BeFalse("the audit log rolls back with the rows");
        }
    }

    [Fact]
    public async Task DeletingAnActivity_RemovesItsTransactionsTogether_WithAuditLog_AndReceiptsBestEffort()
    {
        var (_, activityId, transactionId) = await SeedReportAsync(receipt: true);
        int second;
        await using (var db = await Db())
        {
            var t = Transaction.Create(new DateTime(2026, 1, 11), "Bebidas", "Comida", 5m, "Expense", activityId,
                _factory.Receipts.Put($"receipts/test/b_{Guid.NewGuid():N}.png"));
            db.Transactions.Add(t);
            await db.SaveChangesAsync();
            second = t.Id;
        }

        var (admin, _) = await SignInAsync(null, "Admin");
        await WithTokenAsync(admin);
        _factory.Receipts.FailDeletes = true;
        try
        {
            (await admin.DeleteAsync($"/api/treasury/activities/{activityId}")).StatusCode.Should().Be(HttpStatusCode.OK,
                "a storage failure after the commit does not fail the delete");
        }
        finally
        {
            _factory.Receipts.FailDeletes = false;
        }

        (await TransactionAsync(transactionId)).Should().BeNull();
        (await TransactionAsync(second)).Should().BeNull();
        await using (var db = await Db())
        {
            (await db.Activities.AnyAsync(a => a.Id == activityId)).Should().BeFalse();
            (await db.AuditLogs.Where(l => l.EntityType == "Transaction" && l.Action == "Deleted" && (l.EntityId == transactionId || l.EntityId == second)).CountAsync())
                .Should().Be(2, "each deleted transaction is in the audit log, as before");
        }
    }

    private static JsonElement ActivityOf(JsonElement report, int id) =>
        report.GetProperty("activities").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == id);

    private async Task<Transaction?> TransactionAsync(int id)
    {
        await using var db = await Db();
        return await db.Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
    }
}
