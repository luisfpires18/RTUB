// PushApiTests: Preferences tests.
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Integration.Tests.Api;

public partial class PushApiTests
{
    // Task 082 (#263): account-wide preferences, apart from the device subscription.

    [Fact]
    public async Task Preferences_StartAllOn_WithOnlyThePendingActionsReminderLocked()
    {
        var (member, _) = await SignInAsync();

        var response = await member.GetAsync("/api/push/preferences");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var items = (await AreaHttp.Json(member, "/api/push/preferences")).GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCount(RTUB.Application.Services.PushPreferenceCatalog.Items.Count);
        items.Should().OnlyContain(i => i.GetProperty("enabled").GetBoolean());
        items.Where(i => i.GetProperty("mandatory").GetBoolean()).Select(i => i.GetProperty("key").GetString())
            .Should().Equal("pending-actions.daily");
        items.Select(i => i.GetProperty("key").GetString()).Should().Contain(["events.new", "summary.weekly", "owner.announcement"]);
        items.Should().OnlyContain(i => i.GetProperty("label").GetString()!.Length > 0 && i.GetProperty("group").GetString()!.Length > 0);
    }

    [Fact]
    public async Task Preferences_SaveReplacesTheMembersSet_PersistAcrossReads_AndNeverTouchAnotherMember()
    {
        var (member, user) = await SignInAsync();
        var (other, otherUser) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        var saved = await AreaHttp.Json(await member.PutAsJsonAsync("/api/push/preferences", new { disabled = new[] { "events.new", "summary.weekly", "events.new" } }));
        Off(saved).Should().BeEquivalentTo("events.new", "summary.weekly");
        Off(await AreaHttp.Json(member, "/api/push/preferences")).Should().BeEquivalentTo(new[] { "events.new", "summary.weekly" }, "it is stored per account");
        (await WithDbAsync(db => db.PushPreferences.CountAsync(p => p.UserId == user.Id))).Should().Be(2, "a duplicate in the request is one row");

        // The next save is the whole set: events.new comes back on, messages goes off.
        var next = await AreaHttp.Json(await member.PutAsJsonAsync("/api/push/preferences", new { disabled = new[] { "summary.weekly", "messages" } }));
        Off(next).Should().BeEquivalentTo("summary.weekly", "messages");

        // Everything back on: no rows left.
        Off(await AreaHttp.Json(await member.PutAsJsonAsync("/api/push/preferences", new { disabled = Array.Empty<string>() }))).Should().BeEmpty();
        (await WithDbAsync(db => db.PushPreferences.CountAsync(p => p.UserId == user.Id))).Should().Be(0);

        Off(await AreaHttp.Json(other, "/api/push/preferences")).Should().BeEmpty("another member's preferences are untouched");
        otherUser.Id.Should().NotBe(user.Id);
    }

    [Fact]
    public async Task Preferences_RefuseTheMandatoryKey_AndUnknownKeys_AndNeedTheAntiforgeryToken()
    {
        var (member, user) = await SignInAsync();

        (await member.PutAsJsonAsync("/api/push/preferences", new { disabled = new[] { "events.new" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");
        await AreaHttp.WithTokenAsync(member);

        var locked = await member.PutAsJsonAsync("/api/push/preferences", new { disabled = new[] { "events.new", "pending-actions.daily" } });
        locked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AreaHttp.Errors(locked)).Keys.Should().Equal("disabled");

        var unknown = await member.PutAsJsonAsync("/api/push/preferences", new { disabled = new[] { "Novas atuações" } });
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a translated title is not a key");

        // A refused save stores nothing, not even the valid part of it.
        (await WithDbAsync(db => db.PushPreferences.CountAsync(p => p.UserId == user.Id))).Should().Be(0);
        Off(await AreaHttp.Json(member, "/api/push/preferences")).Should().BeEmpty();
    }

    [Fact]
    public async Task Preferences_DoNotTouchTheDeviceSubscription_AndTheSubscriptionDoesNotTouchThem()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        var endpoint = Endpoint();
        (await member.PostAsJsonAsync("/api/push/subscribe", Subscription(endpoint))).EnsureSuccessStatusCode();

        (await member.PutAsJsonAsync("/api/push/preferences", new { disabled = new[] { "events.new" } })).EnsureSuccessStatusCode();
        var status = await AreaHttp.Json(member, "/api/push/status");
        status.GetProperty("subscriptions").GetInt32().Should().Be(1, "a content preference never unsubscribes a device");
        status.GetProperty("optedOut").GetBoolean().Should().BeFalse();

        (await member.PostAsJsonAsync("/api/push/unsubscribe", new { all = true })).EnsureSuccessStatusCode();
        Off(await AreaHttp.Json(member, "/api/push/preferences")).Should().Equal(new[] { "events.new" }, "turning a device off keeps the account's choices");
    }
}
