using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// Tesouraria on /api/treasury (React track 024; were the Blazor /finance, /finance/report/{id}, /calotes, /mbway and
/// /nerba pages) through the real host: real login, roles, antiforgery and SQLite. Receipts go to
/// <see cref="FakeReceiptStorage"/> (in memory, nothing reaches R2); push and e-mail are recording mocks and the daily calote
/// reminder is switched off, so nothing is ever sent. Every test makes its own reports, fiscal years and events, since the
/// class shares one database.
/// </summary>
public partial class TreasuryApiTests : IClassFixture<TreasuryApiFactory>
{
    private readonly TreasuryApiFactory _factory;

    public TreasuryApiTests(TreasuryApiFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------- helpers

    private static int _unique;

    private static int Unique(int floor) => floor + Interlocked.Increment(ref _unique);

    private static string Label(int startYear) => $"{startYear}-{startYear + 1}";

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

    private async Task<int> AddFiscalYearAsync()
    {
        await using var db = await Db();
        var start = Unique(1300);
        var year = FiscalYear.Create(start, start + 1);
        db.FiscalYears.Add(year);
        await db.SaveChangesAsync();
        return year.StartYear;
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

    private async Task<MemberDebt?> DebtAsync(int id)
    {
        await using var db = await Db();
        return await db.MemberDebts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
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

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
