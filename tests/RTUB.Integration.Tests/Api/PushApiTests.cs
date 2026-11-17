using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The member's own push subscriptions, /api/push (task 038, docs/react-push-v2.md), over the real
/// PushNotificationService, repository and SQLite. The WebPush options are one mutable instance, so a test can switch
/// the feature off or remove the keys (tests in a class run one at a time). No push is ever sent here.
/// </summary>
public partial class PushApiTests : IClassFixture<PushApiFactory>
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
        (await visitor.GetAsync("/api/push/reminder")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.GetAsync("/api/push/preferences")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.PutAsJsonAsync("/api/push/preferences", new { disabled = Array.Empty<string>() })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var path in new[] { "/api/push/check", "/api/push/subscribe", "/api/push/unsubscribe", "/api/push/test", "/api/push/reminder/ack" })
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

    private static string[] Off(JsonElement answer) =>
        answer.GetProperty("items").EnumerateArray().Where(i => !i.GetProperty("enabled").GetBoolean()).Select(i => i.GetProperty("key").GetString()!).ToArray();

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
