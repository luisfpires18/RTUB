using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The member's own push subscriptions, /api/push (task 038, docs/react-push-v2.md), over the real
/// PushNotificationService, repository and SQLite. The WebPush options are one mutable instance, so a test can switch
/// the feature off or remove the keys (tests in a class run one at a time). No push is ever sent here.
/// </summary>
public class PushApiTests : IClassFixture<PushApiFactory>
{
    private readonly PushApiFactory _factory;
    private static int _ip;

    public PushApiTests(PushApiFactory factory)
    {
        _factory = factory;
        _factory.Reset();
    }

    [Fact]
    public async Task Visitors_Get401_OnEveryRoute()
    {
        var visitor = AreaHttp.Anonymous(_factory, "10.38.250.1");

        (await visitor.GetAsync("/api/push/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AreaHttp.WithTokenAsync(visitor);
        foreach (var path in new[] { "/api/push/check", "/api/push/subscribe", "/api/push/unsubscribe", "/api/push/test" })
        {
            (await visitor.PostAsJsonAsync(path, new { endpoint = Endpoint() })).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }
    }

    [Fact]
    public async Task Status_GivesThePublicKeyOnly_NeverThePrivateOne_AndIsNoStore()
    {
        var (member, _) = await SignInAsync();

        var response = await member.GetAsync("/api/push/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain(PushApiFactory.PrivateKey).And.NotContain("mailto:");

        var status = JsonDocument.Parse(text).RootElement;
        status.GetProperty("enabled").GetBoolean().Should().BeTrue();
        status.GetProperty("configured").GetBoolean().Should().BeTrue();
        status.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);
        status.GetProperty("vapidPublicKey").GetString().Should().Be(PushApiFactory.PublicKey);
        status.GetProperty("subscriptions").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Status_SaysWhyPushIsOff_WithoutAKey()
    {
        var (member, _) = await SignInAsync();

        _factory.Options.Enabled = false;
        var off = await AreaHttp.Json(member, "/api/push/status");
        off.GetProperty("enabled").GetBoolean().Should().BeFalse();
        off.GetProperty("reason").GetString().Should().Be("disabled");
        off.GetProperty("vapidPublicKey").ValueKind.Should().Be(JsonValueKind.Null);

        // The Owner always has push (WebPush:Enabled is for everyone else), as before.
        var (owner, _) = await SignInAsync("Owner");
        (await AreaHttp.Json(owner, "/api/push/status")).GetProperty("enabled").GetBoolean().Should().BeTrue();

        _factory.Options.Enabled = true;
        _factory.Options.VapidPrivateKey = "";
        var unconfigured = await AreaHttp.Json(member, "/api/push/status");
        unconfigured.GetProperty("reason").GetString().Should().Be("not-configured");
        unconfigured.GetProperty("vapidPublicKey").ValueKind.Should().Be(JsonValueKind.Null);

        await AreaHttp.WithTokenAsync(member);
        var refused = await member.PostAsJsonAsync("/api/push/subscribe", Subscription(Endpoint()));
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        _factory.Options.Enabled = false;
        _factory.Options.VapidPrivateKey = PushApiFactory.PrivateKey;
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(Endpoint()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryToken()
    {
        var (member, _) = await SignInAsync();

        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(Endpoint()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsJsonAsync("/api/push/unsubscribe", new { all = true })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A cross-site form cannot even reach them: no form content type binds.
        await AreaHttp.WithTokenAsync(member);
        var form = await member.PostAsync("/api/push/unsubscribe", new FormUrlEncodedContent(new Dictionary<string, string> { ["all"] = "true" }));
        ((int)form.StatusCode).Should().BeOneOf(400, 415);
    }

    [Fact]
    public async Task Subscribe_ValidatesTheBrowserSubscription()
    {
        var (member, user) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        async Task<string[]> Errors(object body) => (await AreaHttp.Errors(await member.PostAsJsonAsync("/api/push/subscribe", body))).Keys.ToArray();

        (await Errors(new { endpoint = "http://fcm.googleapis.com/fcm/send/x", keys = Keys() })).Should().Equal("endpoint");
        (await Errors(new { endpoint = "https://169.254.169.254/latest", keys = Keys() })).Should().Equal("endpoint");
        (await Errors(new { endpoint = "https://localhost/push", keys = Keys() })).Should().Equal("endpoint");
        (await Errors(new { endpoint = "https://evil.example/fcm.googleapis.com", keys = Keys() })).Should().Equal("endpoint");
        (await Errors(new { endpoint = "https://fcm.googleapis.com.evil.example/x", keys = Keys() })).Should().Equal("endpoint");
        (await Errors(new { endpoint = Endpoint(), keys = new { p256dh = "not base64!", auth = Key(16) } })).Should().Equal("p256dh");
        (await Errors(new { endpoint = Endpoint(), keys = new { p256dh = Key(65, 0x02), auth = Key(16) } })).Should().Equal("p256dh");
        (await Errors(new { endpoint = Endpoint(), keys = new { p256dh = Key(65), auth = Key(8) } })).Should().Equal("auth");
        (await Errors(new { endpoint = Endpoint() })).Should().BeEquivalentTo("p256dh", "auth");

        (await WithDbAsync(db => db.PushSubscriptions.CountAsync(s => s.UserId == user.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Subscribe_StoresTheMembersDevice_OnceEvenIfSentTwice_AndAcceptsEveryBrowserService()
    {
        var (member, user) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        var endpoint = Endpoint();

        var first = await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/subscribe", Subscription(endpoint, expirationTime: 1_900_000_000_000)));
        first.GetProperty("subscriptions").GetInt32().Should().Be(1);
        first.GetProperty("optedOut").GetBoolean().Should().BeFalse();
        (await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/subscribe", Subscription(endpoint))))
            .GetProperty("subscriptions").GetInt32().Should().Be(1, "the same endpoint is updated, never duplicated");

        foreach (var other in new[]
                 {
                     $"https://updates.push.services.mozilla.com/wpush/v2/{Guid.NewGuid():N}",
                     $"https://web.push.apple.com/{Guid.NewGuid():N}",
                     $"https://wns2-par02p.notify.windows.com/w/?token={Guid.NewGuid():N}",
                 })
        {
            (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(other))).StatusCode.Should().Be(HttpStatusCode.OK, other);
        }

        var rows = await WithDbAsync(db => db.PushSubscriptions.AsNoTracking().Where(s => s.UserId == user.Id).ToListAsync());
        rows.Should().HaveCount(4);
        rows.Single(r => r.Endpoint == endpoint).P256dh.Should().NotContain("=").And.NotContain("+").And.NotContain("/");

        var check = await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/check", new { endpoint }));
        check.GetProperty("registered").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_WithReplaces_DropsTheSameDevicesOldEndpoint_ButNeverAnotherMembers()
    {
        var (member, user) = await SignInAsync();
        var (other, otherUser) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        await AreaHttp.WithTokenAsync(other);
        var old = Endpoint();
        var theirs = Endpoint();
        (await other.PostAsJsonAsync("/api/push/subscribe", Subscription(theirs))).EnsureSuccessStatusCode();
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(old))).EnsureSuccessStatusCode();

        var rotated = Endpoint();
        var status = await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/subscribe", Subscription(rotated, replaces: old)));
        status.GetProperty("subscriptions").GetInt32().Should().Be(1);
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(Endpoint(), replaces: theirs))).EnsureSuccessStatusCode();

        (await WithDbAsync(db => db.PushSubscriptions.AnyAsync(s => s.Endpoint == old))).Should().BeFalse();
        (await WithDbAsync(db => db.PushSubscriptions.AnyAsync(s => s.Endpoint == theirs && s.UserId == otherUser.Id))).Should().BeTrue(
            "replaces only ever removes the caller's own subscription");
        (await AreaHttp.Json(await other.PostAsJsonAsync("/api/push/check", new { endpoint = old }))).GetProperty("registered").GetBoolean().Should().BeFalse();
        (await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/check", new { endpoint = theirs }))).GetProperty("registered").GetBoolean()
            .Should().BeFalse("another member's endpoint is not this member's");
        user.Id.Should().NotBe(otherUser.Id);
    }

    [Fact]
    public async Task Unsubscribe_RemovesOnlyTheCallersDevice_OptsOutWhenNoneIsLeft_AndAllRemovesEveryOne()
    {
        var (member, user) = await SignInAsync();
        var (other, otherUser) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        await AreaHttp.WithTokenAsync(other);
        var phone = Endpoint();
        var laptop = Endpoint();
        var theirs = Endpoint();
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(phone))).EnsureSuccessStatusCode();
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(laptop))).EnsureSuccessStatusCode();
        (await other.PostAsJsonAsync("/api/push/subscribe", Subscription(theirs))).EnsureSuccessStatusCode();

        (await AreaHttp.Errors(await member.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = " " }))).Keys.Should().Equal("endpoint");

        // Another member's endpoint is not touched (the old endpoint deleted any subscription by endpoint).
        var notMine = await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = theirs }));
        notMine.GetProperty("subscriptions").GetInt32().Should().Be(2);
        (await WithDbAsync(db => db.PushSubscriptions.AnyAsync(s => s.Endpoint == theirs && s.UserId == otherUser.Id))).Should().BeTrue();

        var one = await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = phone }));
        one.GetProperty("subscriptions").GetInt32().Should().Be(1);
        one.GetProperty("optedOut").GetBoolean().Should().BeFalse("the laptop is still on");

        var all = await AreaHttp.Json(await member.PostAsJsonAsync("/api/push/unsubscribe", new { all = true }));
        all.GetProperty("subscriptions").GetInt32().Should().Be(0);
        all.GetProperty("optedOut").GetBoolean().Should().BeTrue();
        (await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id))).PushNotificationsOptedOut.Should().BeTrue();

        // Turning it on again clears the opt-out; removing works even with push switched off.
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(phone))).EnsureSuccessStatusCode();
        (await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id))).PushNotificationsOptedOut.Should().BeFalse();
        _factory.Options.Enabled = false;
        (await member.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = phone })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.PushSubscriptions.CountAsync(s => s.UserId == user.Id))).Should().Be(0);
    }

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

    private static string Endpoint() => $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}";

    /// <summary>A key of the right length; standard base64 with padding, as some Android browsers send it.</summary>
    private static string Key(int length, byte first = 0x04)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        bytes[0] = first;
        return Convert.ToBase64String(bytes);
    }

    private static object Keys() => new { p256dh = Key(65), auth = Key(16) };

    private static object Subscription(string endpoint, long? expirationTime = null, string? replaces = null) =>
        new { endpoint, keys = Keys(), expirationTime, replaces };

    private Task<(HttpClient Client, RTUB.Core.Entities.ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"psh{Guid.NewGuid():N}"[..20], $"10.38.{n / 250}.{n % 250 + 1}", role);
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}

public sealed class PushApiFactory : TestWebApplicationFactory
{
    /// <summary>A well-formed P-256 public key shape; never used to send.</summary>
    public static readonly string PublicKey = Base64Url.EncodeToString(Enumerable.Repeat((byte)4, 1).Concat(Enumerable.Range(1, 64).Select(i => (byte)i)).ToArray());

    /// <summary>A sentinel: the tests assert it never appears in an answer.</summary>
    public const string PrivateKey = "PRIVATE-VAPID-SENTINEL-must-never-leave-the-server";

    public WebPushOptions Options { get; } = new();

    public void Reset()
    {
        Options.Enabled = true;
        Options.VapidSubject = "mailto:push-tests@rtub.invalid";
        Options.VapidPublicKey = PublicKey;
        Options.VapidPrivateKey = PrivateKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Reset();
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOptions<WebPushOptions>>();
            services.AddSingleton<IOptions<WebPushOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        });
    }
}
