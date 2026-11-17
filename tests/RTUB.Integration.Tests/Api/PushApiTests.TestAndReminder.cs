// PushApiTests: TestAndReminder tests.
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class PushApiTests
{
    [Fact]
    public async Task Test_NeedsTheToken_AndOnlyReachesTheCallersOwnDevice_OncePerCooldown()
    {
        var (member, _) = await SignInAsync();
        var (other, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(other);
        var theirs = Endpoint();
        (await other.PostAsJsonAsync("/api/push/subscribe", Subscription(theirs))).EnsureSuccessStatusCode();

        (await member.PostAsJsonAsync("/api/push/test", new { endpoint = theirs })).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "no antiforgery token");

        // Another member's device is never sent to (nothing reaches a push service in this test).
        await AreaHttp.WithTokenAsync(member);
        var response = await member.PostAsJsonAsync("/api/push/test", new { endpoint = theirs });
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var result = await AreaHttp.Json(response);
        result.GetProperty("outcome").GetString().Should().Be("not-registered");
        result.GetProperty("status").ValueKind.Should().Be(JsonValueKind.Null);

        (await member.PostAsJsonAsync("/api/push/test", new { endpoint = theirs })).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Test_IsRefused_WhenPushIsOffOrUnconfigured_OrWithoutAnEndpoint()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        (await AreaHttp.Errors(await member.PostAsJsonAsync("/api/push/test", new { endpoint = " " }))).Keys.Should().Equal("endpoint");

        _factory.Options.VapidPrivateKey = "";
        (await member.PostAsJsonAsync("/api/push/test", new { endpoint = Endpoint() })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        _factory.Options.VapidPrivateKey = PushApiFactory.PrivateKey;
        _factory.Options.Enabled = false;
        (await member.PostAsJsonAsync("/api/push/test", new { endpoint = Endpoint() })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheOldControllerOnlyRoutes_AreGone()
    {
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);

        // Sending is the Owner's /notifications (/api/owner/notifications/*, task 035); the old Admin-reachable
        // controller routes had no caller left.
        (await owner.PostAsJsonAsync("/api/push/broadcast", new { title = "x", body = "y" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.PostAsJsonAsync("/api/push/send-to-selected", new { userIds = new[] { "x" } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    [Fact]
    public async Task Reminder_IsTheCallersOwn_NeedsTheToken_FollowsTheAccount_AndIgnoresConcurrentTabs()
    {
        var (active, _) = await SignInAsync();
        var (reformed, user) = await SignInAsync();
        await WithDbAsync(async db =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == user.Id);
            u.IsRetired = true;
            return await db.SaveChangesAsync();
        });

        var first = await member(reformed);
        first.GetProperty("reformed").GetBoolean().Should().BeTrue();
        first.GetProperty("acknowledged").GetBoolean().Should().BeFalse();

        (await reformed.PostAsync("/api/push/reminder/ack", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");
        (await member(reformed)).GetProperty("acknowledged").GetBoolean().Should().BeFalse();

        await AreaHttp.WithTokenAsync(reformed);
        var acks = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => reformed.PostAsync("/api/push/reminder/ack", null)));
        acks.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent, "a second tab storing it too is fine");
        (await member(reformed)).GetProperty("acknowledged").GetBoolean().Should().BeTrue();
        (await WithDbAsync(db => db.PendingActionDismissals.CountAsync(d => d.UserId == user.Id && d.ActionKind == "startup.push")))
            .Should().Be(1);

        // Another member's state is untouched.
        var other = await member(active);
        other.GetProperty("reformed").GetBoolean().Should().BeFalse();
        other.GetProperty("acknowledged").GetBoolean().Should().BeFalse();

        static Task<JsonElement> member(HttpClient client) => AreaHttp.Json(client, "/api/push/reminder");
    }
}
