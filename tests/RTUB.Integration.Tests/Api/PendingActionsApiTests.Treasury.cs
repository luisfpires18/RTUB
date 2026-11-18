// PendingActionsApiTests tests: treasurer panels.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Integration.Tests.Api;
public partial class PendingActionsApiTests
{
    // ---------- Tesouraria (074) ----------

    [Fact]
    public async Task Treasurers_ShareOneRequiredReport_AndParallelClicksMakeOne()
    {
        await DeleteCurrentReportAsync();
        var fy = CurrentFiscalYearStart();
        var (first, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.PrimeiroTesoureiro]);
        var (second, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.SegundoTesoureiro]);
        var (admin, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin");
        await AreaHttp.WithTokenAsync(first);
        await AreaHttp.WithTokenAsync(second);
        await AreaHttp.WithTokenAsync(admin);

        var row = (await PanelAsync(first, "treasury", "open")).GetProperty("rows").EnumerateArray().Single();
        row.GetProperty("required").GetBoolean().Should().BeTrue();
        row.GetProperty("sourceKey").GetString().Should().Be($"report:{fy}");
        (await PanelAsync(second, "treasury", "open")).GetProperty("rows").EnumerateArray().Single().GetProperty("sourceKey").GetString()
            .Should().Be($"report:{fy}", "one shared action for both treasurers");
        (await admin.GetAsync("/api/pending-actions/panels/treasury")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a role without the position");
        (await Command(admin, "treasury", $"report:{fy}", "create")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await first.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "treasury", sourceKey = $"report:{fy}" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a required action cannot be dismissed");

        var clicks = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Command(i % 2 == 0 ? first : second, "treasury", $"report:{fy}", "create")));

        clicks.Count(c => c.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        clicks.Where(c => c.StatusCode != HttpStatusCode.NoContent).Select(c => c.StatusCode).Should()
            .OnlyContain(c => c == HttpStatusCode.NotFound || c == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var reports = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reports.AsNoTracking().Where(r => r.Year == fy).ToListAsync();
        reports.Should().ContainSingle().Which.Title.Should().Be($"Relatório de Contas {fy} - {fy + 1}", "the report page's own creation");
        (await PanelAsync(second, "treasury", "open")).GetProperty("rows").EnumerateArray()
            .Should().NotContain(r => r.GetProperty("sourceKey").GetString() == $"report:{fy}", "resolved for both");
    }

    [Fact]
    public async Task Treasurers_ClassifyEachBegunEvent_WithAnActivityOrASharedSemMovimentos()
    {
        Assert.SkipWhen(PortugalTime.Today(TimeProvider.System).Month == 9 && PortugalTime.Today(TimeProvider.System).Day == 1,
            "1 September: yesterday is in the previous fiscal year");
        var reportId = await EnsureCurrentReportAsync();
        var (first, firstUser) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.PrimeiroTesoureiro]);
        var (second, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.SegundoTesoureiro]);
        await AreaHttp.WithTokenAsync(first);
        await AreaHttp.WithTokenAsync(second);
        var begun = await AddEventAsync(-1);
        var another = await AddEventAsync(-1);
        var future = await AddEventAsync(NextDay());
        var cancelled = await AddEventAsync(-1);
        await CancelEventAsync(cancelled);

        var open = await AllKeysAsync(first, "treasury", "open");
        open.Should().Contain([$"event:{begun}", $"event:{another}"]).And.NotContain([$"event:{future}", $"event:{cancelled}"]);
        var row = await RowAsync(first, "treasury", "open", $"event:{begun}");
        row.GetProperty("actions")[0].GetProperty("href").GetString().Should().Be($"/treasury/reports/{reportId}?activityForEvent={begun}");

        // Não: shared, for both treasurers, and listed under "Sem movimentos" where it can be reopened.
        (await Command(first, "treasury", $"event:{begun}", "none")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AllKeysAsync(second, "treasury", "open")).Should().NotContain($"event:{begun}");
        (await AllKeysAsync(second, "treasury", "none")).Should().Contain($"event:{begun}");
        (await Command(second, "treasury", $"event:{begun}", "none")).StatusCode.Should().Be(HttpStatusCode.NotFound, "already decided");
        (await Command(second, "treasury", $"event:{begun}", "reopen")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AllKeysAsync(first, "treasury", "open")).Should().Contain($"event:{begun}");

        // Sim: an activity of this year's report linked to the event, on the report page; deleting it reopens the question.
        var created = await first.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities",
            new { name = "Serenata", startDate = DateTime.Today.AddDays(-1), endDate = (DateTime?)null, description = "", eventId = another });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activities").EnumerateArray()
            .Single(a => a.ValueKind == JsonValueKind.Object && a.GetProperty("eventId").ValueKind == JsonValueKind.Number && a.GetProperty("eventId").GetInt32() == another);
        (await AllKeysAsync(second, "treasury", "open")).Should().NotContain($"event:{another}", "an activity accounts for it");
        (await first.DeleteAsync($"/api/treasury/activities/{activity.GetProperty("id").GetInt32()}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AllKeysAsync(second, "treasury", "open")).Should().Contain($"event:{another}", "deleting the activity re-evaluates it");
        (await first.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities",
                new { name = "Fora do ano", startDate = DateTime.Today, endDate = (DateTime?)null, description = "", eventId = cancelled }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a cancelled event cannot be linked");

        // A treasurer whose mandate ended no longer has the panel or its commands.
        await SetPositionsAsync(firstUser.Id, []);
        (await first.GetAsync("/api/pending-actions/panels/treasury")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Command(first, "treasury", $"event:{another}", "none")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ATreasurerWithoutTheModulesRole_IsRefusedByTheModule()
    {
        Assert.SkipWhen(PortugalTime.Today(TimeProvider.System).Month == 9 && PortugalTime.Today(TimeProvider.System).Day == 1,
            "1 September: yesterday is in the previous fiscal year");
        await EnsureCurrentReportAsync();
        var (treasurer, _) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.PrimeiroTesoureiro]);
        await AreaHttp.WithTokenAsync(treasurer);
        var e = await AddEventAsync(-1);

        (await AllKeysAsync(treasurer, "treasury", "open")).Should().Contain($"event:{e}", "the mandate shows the work");
        (await Command(treasurer, "treasury", $"event:{e}", "none")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the treasury's report rule decides the write");
        (await AllKeysAsync(treasurer, "treasury", "open")).Should().Contain($"event:{e}");
    }

    private async Task DeleteCurrentReportAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fy = CurrentFiscalYearStart();
        db.Activities.RemoveRange(db.Activities.Where(a => a.Report!.Year == fy));
        db.Reports.RemoveRange(db.Reports.Where(r => r.Year == fy));
        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureCurrentReportAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fy = CurrentFiscalYearStart();
        if (await db.Reports.FirstOrDefaultAsync(r => r.Year == fy) is { } existing)
        {
            return existing.Id;
        }

        var report = Report.Create($"Relatório de Contas {fy} - {fy + 1}", fy);
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }

    private static Task<JsonElement> PanelAsync(HttpClient client, string panel, string? filter) =>
        client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter ?? ""}&page=1");
}
