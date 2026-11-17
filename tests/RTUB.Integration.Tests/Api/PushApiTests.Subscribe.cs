// PushApiTests: Subscribe tests.
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Integration.Tests.Api;

public partial class PushApiTests
{
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
}
