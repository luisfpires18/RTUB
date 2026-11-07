using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Data;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Task 083 (#264): /api/releases/notes over a fixture bundle (2.1.9, 2.1.10, 2.2.0, 2.3.0) and a build that says it
/// is 2.2.0. Visitors get nothing; a member sees the pending releases newest first and acknowledges only the running
/// version; the receipt is per member, follows the account, and a newer unseen release or an older one is refused.
/// </summary>
public class ReleaseNotesApiTests : IClassFixture<ReleaseNotesApiFactory>
{
    private readonly ReleaseNotesApiFactory _factory;
    private static int _ip;

    public ReleaseNotesApiTests(ReleaseNotesApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Visitors_Get401()
    {
        var visitor = AreaHttp.Anonymous(_factory, "10.39.250.1");
        (await visitor.GetAsync("/api/releases/notes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AreaHttp.WithTokenAsync(visitor);
        (await visitor.PostAsJsonAsync("/api/releases/notes/ack", new { version = "2.2.0" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_SeesTheRunningReleaseAndOlderUnread_NewestFirst_NeverANewerFile()
    {
        var (member, _) = await SignInAsync();

        var response = await member.GetAsync("/api/releases/notes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var state = await AreaHttp.Json(member, "/api/releases/notes");

        state.GetProperty("version").GetString().Should().Be("2.2.0");
        state.GetProperty("current").GetProperty("title").GetString().Should().Be("Nova atualização 2.2.0");
        Versions(state).Should().Equal(new[] { "2.2.0", "2.1.10", "2.1.9" }, "SemVer order, and 2.3.0 is newer than this build");
    }

    [Fact]
    public async Task Entendido_StoresOneReceiptForTheRunningVersion_PerMember_AndCoversTheOlderOnes()
    {
        var (member, user) = await SignInAsync();
        var (other, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        var acks = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => member.PostAsJsonAsync("/api/releases/notes/ack", new { version = "2.2.0" })));
        acks.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent, "a second tab storing it too is fine");

        Versions(await AreaHttp.Json(member, "/api/releases/notes")).Should().BeEmpty("nothing is pending after Entendido");
        (await AreaHttp.Json(member, "/api/releases/notes")).GetProperty("current").GetProperty("version").GetString()
            .Should().Be("2.2.0", "the footer can still reopen the running release's notes");
        (await WithDbAsync(db => db.PendingActionDismissals.CountAsync(d => d.UserId == user.Id && d.ActionKind == ReleaseNotesService.AckKind)))
            .Should().Be(1);

        Versions(await AreaHttp.Json(other, "/api/releases/notes")).Should().Equal(new[] { "2.2.0", "2.1.10", "2.1.9" }, "another member on the same device has their own receipt");
    }

    [Fact]
    public async Task Ack_RefusesAnyVersionButTheRunningOne_AndNeedsTheToken()
    {
        var (member, user) = await SignInAsync();

        (await member.PostAsJsonAsync("/api/releases/notes/ack", new { version = "2.2.0" })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");
        await AreaHttp.WithTokenAsync(member);

        foreach (var version in new[] { "2.3.0", "2.1.10", "9.9.9", "", "Nova atualização 2.2.0" })
        {
            var refused = await member.PostAsJsonAsync("/api/releases/notes/ack", new { version });
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, version);
            (await AreaHttp.Errors(refused)).Keys.Should().Equal("version");
        }

        (await WithDbAsync(db => db.PendingActionDismissals.CountAsync(d => d.UserId == user.Id && d.ActionKind == ReleaseNotesService.AckKind))).Should().Be(0);
        Versions(await AreaHttp.Json(member, "/api/releases/notes")).Should().HaveCount(3, "a refused acknowledgement stores nothing");
    }

    [Fact]
    public async Task AnOlderReceipt_LeavesOnlyTheNewerReleasesPending_AndANewerReceiptHidesTheRunningOne()
    {
        var (behind, behindUser) = await SignInAsync();
        var (ahead, aheadUser) = await SignInAsync();
        await WithDbAsync(async db =>
        {
            // As the app itself stores them: a member last saw 2.1.9 (semver, so 2.1.10 is still unread), another
            // acknowledged 2.3.0 on a build that was rolled back to 2.2.0.
            db.PendingActionDismissals.Add(new RTUB.Core.Entities.PendingActionDismissal { UserId = behindUser.Id, ActionKind = ReleaseNotesService.AckKind, SourceKey = "2.1.9", Period = "", DismissedAt = DateTime.UtcNow });
            db.PendingActionDismissals.Add(new RTUB.Core.Entities.PendingActionDismissal { UserId = aheadUser.Id, ActionKind = ReleaseNotesService.AckKind, SourceKey = "2.3.0", Period = "", DismissedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });

        Versions(await AreaHttp.Json(behind, "/api/releases/notes")).Should().Equal("2.2.0", "2.1.10");
        Versions(await AreaHttp.Json(ahead, "/api/releases/notes")).Should().BeEmpty("no false 'new update' after a rollback");
    }

    private static string[] Versions(JsonElement state) =>
        state.GetProperty("pending").EnumerateArray().Select(n => n.GetProperty("version").GetString()!).ToArray();

    private Task<(HttpClient Client, RTUB.Core.Entities.ApplicationUser User)> SignInAsync()
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"rel{Guid.NewGuid():N}"[..20], $"10.39.{n / 250}.{n % 250 + 1}", null);
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}

public sealed class ReleaseNotesApiFactory : TestWebApplicationFactory
{
    public string Notes { get; } = Path.Combine(Path.GetTempPath(), "rtub-notes-api-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Directory.CreateDirectory(Notes);
        foreach (var version in new[] { "2.1.9", "2.1.10", "2.2.0", "2.3.0" })
        {
            File.WriteAllText(Path.Combine(Notes, $"{version}.json"),
                $$"""{"version":"{{version}}","title":"Nova atualização {{version}}","items":["Podes agora testar a {{version}}."]}""");
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ReleaseNotesService>();
            services.AddScoped(sp => new ReleaseNotesService(
                sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(),
                Options.Create(new ReleaseNotesOptions { Directory = Notes }),
                sp.GetRequiredService<ILogger<ReleaseNotesService>>(),
                new BuildInfo("2.2.0", "0123456")));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(Notes)) Directory.Delete(Notes, recursive: true);
    }
}
