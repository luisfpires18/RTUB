using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The documentation on /api/documentation (React track 022, was the Blazor /documentation) through the real host: real
/// login, antiforgery and SQLite. Document storage is <see cref="FakeDocumentStorage"/>, an in-memory bucket: nothing
/// reaches R2. Documentation sends no notifications. The storage is emptied before every test.
/// </summary>
public partial class DocumentationApiTests : IClassFixture<DocumentationApiFactory>
{
    private static readonly string Year = FiscalYearHelper.GetCurrentFiscalYearString();
    private readonly DocumentationApiFactory _factory;

    public DocumentationApiTests(DocumentationApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Objects.Clear();
        _factory.Storage.Broken = false;
        ForgetListings();
    }

    /// <summary>The tests write the fake bucket directly (not through a service), so they drop the shared listing too.</summary>
    private void ForgetListings()
    {
        var trees = _factory.Services.GetRequiredService<DocumentTreeCache>();
        for (var start = FiscalYearHelper.AppYearCreated - 1; start <= FiscalYearHelper.GetCurrentFiscalYearStartYear() + 1; start++)
        {
            trees.Forget($"docs/Test/{start}-{start + 1}/");
        }
    }

    private FakeDocumentStorage Storage => _factory.Storage;

    private static string Key(string folder, string? name = null, string year = "") =>
        $"docs/Test/{(year == "" ? Year : year)}/{folder}/{name}";

    private void Seed()
    {
        Storage.Put(Key("Regulamentos", "Estatutos.pdf"), 1234);
        Storage.Put(Key("Regulamentos", "Anexo.docx"), 2048);
        Storage.Put(Key($"Atas CV {Year}", "Ata CV 1.pdf"), 10);
        Storage.Put(Key($"Atas AG {Year}", "Ata AG 1.pdf"), 10);
        Storage.Put(Key("Logistics/Jantar", "orcamento.xlsx"), 99);
        Storage.Put(Key("Logistics/Arraial", "lista.csv"), 5);
        Storage.Put(Key("Vazia"), 0);

        // 104: members see a Logistics folder only while a board they see owns it.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var name in new[] { "Jantar", "Arraial" }.Where(n => !db.LogisticsBoards.Any(b => b.LegacyFolder == n)))
        {
            var board = LogisticsBoard.Create(name);
            board.LegacyFolder = name;
            db.LogisticsBoards.Add(board);
        }

        db.SaveChanges();
    }

    [Fact]
    public async Task Visitors_GetNothing_AndThePageIsReact()
    {
        Seed();
        var before = Storage.Objects.Keys.OrderBy(k => k).ToList();
        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
        (await anonymous.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/documentation/folders", new { name = "Nova" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/documentation/documents", Upload("Regulamentos", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/documentation/documents?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/documentation");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fdocumentation");

        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/documentation")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Media.Documentation").Should().BeNull("the Blazor documentation page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/documentation", "/shop", "/inventory", "/leaderboard", "/members/manage", "/member/events",
                "/member/gallery", "/member/roles", "/hierarchy" });
        Storage.Objects.Keys.OrderBy(k => k).Should().Equal(before, "nothing was written");
    }

    [Fact]
    public async Task Leitoes_AreRefused_UnlessOwner()
    {
        Seed();
        var (leitao, me) = await SignInAsync();
        await UpdateAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        await AreaHttp.WithTokenAsync(leitao);

        (await leitao.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.GetAsync("/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.PostAsync("/api/documentation/documents", Upload("Regulamentos", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (owner, boss) = await SignInAsync("Owner");
        await UpdateAsync(boss.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        (await owner.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.OK, "Owner was never sent away");
    }

    // ---------- helpers ----------

    private static IEnumerable<string?> Documents(JsonElement body, string folder) =>
        body.GetProperty("folders").EnumerateArray().Single(f => f.GetProperty("name").GetString() == folder)
            .GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("name").GetString());

    private static IEnumerable<string?> Folders(JsonElement body) =>
        body.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("name").GetString());

    private static IEnumerable<string?> Labels(JsonElement body) =>
        body.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("label").GetString());

    private static MultipartFormDataContent Upload(string folder, string fileName, byte[]? bytes = null)
    {
        var content = new ByteArrayContent(bytes ?? new byte[] { 1, 2, 3 });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { new StringContent(folder), "folder" }, { content, "file", fileName } };
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task UpdateAsync(string userId, Action<ApplicationUser> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.Users.SingleAsync(u => u.Id == userId));
        await db.SaveChangesAsync();
    }

    private async Task AddFiscalYearAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.FiscalYears.AnyAsync(f => f.StartYear == start))
        {
            db.FiscalYears.Add(FiscalYear.Create(start, start + 1));
            await db.SaveChangesAsync();
        }
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"doc{Guid.NewGuid():N}"[..20], $"10.91.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.92.0.1");
}
