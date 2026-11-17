// OwnerOperationsApiTests: Tracing tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Integration.Tests.Api;

public partial class OwnerOperationsApiTests
{
    // ---------- /owner/tracing ----------

    [Fact]
    public async Task Tracing_FiltersSearchesDeletesAndExports()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var first = await SeedLogAsync(tag, user: $"u{tag}", critical: true, changes: $"{{\"Name\":\"{tag}-one\"}}");
        var second = await SeedLogAsync(tag, user: $"u{tag}", changes: "{\"Name\":\"two\"}");

        var filters = await AreaHttp.Json(owner, "/api/owner/tracing/filters");
        filters.GetProperty("users").EnumerateArray().Select(u => u.GetString()).Should().Contain($"u{tag}");

        var byUser = await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}");
        byUser.GetProperty("total").GetInt32().Should().Be(2);
        var critical = await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}&critical=true");
        LogIds(critical).Should().Equal(first);
        critical.GetProperty("criticalOnPage").GetInt32().Should().Be(1);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?q={tag}-one")).Should().Equal(first);

        // "Esconder sistema": the status reconciliation (even when stamped with the member) and entries with no member go.
        var system = await SeedLogAsync(tag, user: $"s{tag}", setup: l => l.EntityType = "MemberActivityStatus");
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?user=s{tag}")).Should().Equal(system);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?user=s{tag}&system=hide")).Should().BeEmpty();
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}&system=hide")).Should().BeEquivalentTo([first, second]);
        var noMember = await SeedLogAsync(tag, user: null, changes: $"{{\"Name\":\"{tag}-job\"}}");
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?q={tag}-job")).Should().Equal(noMember);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?q={tag}-job&system=hide")).Should().BeEmpty();

        var export = await owner.GetAsync($"/api/owner/tracing/export?user=u{tag}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        export.Content.Headers.ContentDisposition!.FileName.Should().StartWith("tracing-export-");
        export.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await export.Content.ReadAsStringAsync()).Should().Contain($"{tag}-one");

        (await owner.DeleteAsync($"/api/owner/tracing/{second}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/owner/tracing/{second}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}")).Should().Equal(first);
    }

    [Fact]
    public async Task Tracing_TruncateNeedsTheTypedConfirmation_AndOneUsersTruncateKeepsTheRest()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var mine = await SeedLogAsync(tag, user: $"a{tag}");
        var theirs = await SeedLogAsync(tag, user: $"b{tag}");

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = $"a{tag}", confirmation = "delete all" })))
            .Keys.Should().Equal("confirmation");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = "", confirmation = "" })))
            .Keys.Should().Equal("confirmation");
        (await LogExistsAsync(mine)).Should().BeTrue();

        (await owner.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = $"a{tag}", confirmation = "DELETE ALL" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await LogExistsAsync(mine)).Should().BeFalse();
        (await LogExistsAsync(theirs)).Should().BeTrue();
    }

    /// <summary>
    /// #220: the search also matches the stored entity name and narrows the other filters (exact total); messaging
    /// entries stay out of the list, the search, the filters and the export.
    /// </summary>
    [Fact]
    public async Task Tracing_SearchMatchesTheStoredName_WithTheOtherFilters_AndMessagingStaysOut()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var named = await SeedLogAsync(tag, user: $"u{tag}", setup: l => l.EntityDisplayName = $"Arraial {tag}");
        await SeedLogAsync(tag, user: $"v{tag}", setup: l => l.EntityDisplayName = $"Arraial {tag} (outro)");
        var hidden = await SeedLogAsync(tag, user: $"u{tag}", setup: l =>
        {
            l.EntityType = "Message";
            l.EntityDisplayName = $"Arraial {tag} privado";
        });

        var page = await AreaHttp.Json(owner, $"/api/owner/tracing?q=Arraial%20{tag}&user=u{tag}");
        LogIds(page).Should().Equal(named);
        page.GetProperty("total").GetInt32().Should().Be(1, "the search total is exact now");
        (await AreaHttp.Json(owner, $"/api/owner/tracing?q=Arraial%20{tag}")).GetProperty("total").GetInt32().Should().Be(2);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?q=privado&user=u{tag}")).Should().BeEmpty();
        (await AreaHttp.Json(owner, "/api/owner/tracing/filters")).GetProperty("entityTypes").EnumerateArray()
            .Select(t => t.GetString()).Should().NotContain(new[] { "Message", "Conversation", "ConversationUserSettings", "MessageReaction" });
        (await (await owner.GetAsync($"/api/owner/tracing/export?user=u{tag}")).Content.ReadAsStringAsync()).Should().NotContain("privado");
        (await LogExistsAsync(hidden)).Should().BeTrue("excluded, not deleted");
    }

    /// <summary>
    /// #220 hybrid for old entries: a stored member id becomes today's nickname and a missing name the entity's current
    /// title - on the page only. The stored row and the export stay exactly as recorded; a deleted entity stays unnamed.
    /// </summary>
    [Fact]
    public async Task Tracing_OldWeakNames_AreResolvedOnTheReadOnly_NeverSaved_NorExported()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var (memberId, postId) = await WithDbAsync(async db =>
        {
            var member = new ApplicationUser { UserName = $"m{tag}", Email = $"m{tag}@rtub.test", FirstName = "Zé", LastName = "Pires", Nickname = $"Zé {tag}" };
            db.Users.Add(member);
            var post = new NewsPost { Title = $"Novidade {tag}", Body = "Texto" };
            db.NewsPosts.Add(post);
            await db.SaveChangesAsync();
            return (member.Id, post.Id);
        });
        var push = await SeedLogAsync(tag, user: $"u{tag}", setup: l =>
        {
            l.EntityType = "PushSubscription";
            l.EntityDisplayName = memberId;
        });
        var news = await SeedLogAsync(tag, user: $"u{tag}", setup: l =>
        {
            l.EntityType = "NewsPost";
            l.EntityId = postId;
        });
        var gone = await SeedLogAsync(tag, user: $"u{tag}", setup: l =>
        {
            l.EntityType = "NewsPost";
            l.EntityId = 99_999_999;
        });

        var items = (await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}&pageSize=100")).GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetInt32());
        (items[push].GetProperty("entityDisplayName").GetString(), items[push].GetProperty("currentDisplayName").GetString())
            .Should().Be((memberId, $"Zé {tag}"), "the stored value is shown as stored, the nickname beside it");
        items[news].GetProperty("currentDisplayName").GetString().Should().Be($"Novidade {tag}");
        items[news].GetProperty("entityDisplayName").ValueKind.Should().Be(JsonValueKind.Null);
        items[gone].GetProperty("currentDisplayName").ValueKind.Should().Be(JsonValueKind.Null, "a deleted entity cannot be named");

        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().Where(a => a.Id == push || a.Id == news).Select(a => a.EntityDisplayName).ToListAsync()))
            .Should().BeEquivalentTo(new[] { memberId, null }, "nothing is written back");
        var export = await (await owner.GetAsync($"/api/owner/tracing/export?user=u{tag}")).Content.ReadAsStringAsync();
        export.Should().Contain(memberId).And.NotContain($"Zé {tag}").And.NotContain("currentDisplayName");
    }

    /// <summary>
    /// #226 F04: push-subscription entries written before #220 still hold the endpoint and keys in their changes. The page
    /// and the export hide them with the same rule as new entries (AuditConfiguration.RedactedProperties); the stored row
    /// stays byte for byte as recorded, and every other entry and field is untouched.
    /// </summary>
    [Fact]
    public async Task Tracing_OldPushEntries_HideTheEndpointAndKeys_OnThePageAndInTheExport_WithoutRewritingThem()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        const string created = "{\"Auth\":\"auth-secret-1\",\"Endpoint\":\"https://fcm.googleapis.com/fcm/send/endpoint-secret\",\"P256dh\":\"p256-secret\",\"UserAgent\":\"Mozilla/5.0 (Android)\",\"UserId\":\"u-1\"}";
        const string modified = "{\"Auth\":{\"Old\":\"auth-old-secret\",\"New\":\"auth-new-secret\"},\"UserAgent\":{\"Old\":\"a\",\"New\":\"b\"}}";
        const string recent = "{\"Auth\":\"[redacted]\",\"Endpoint\":\"[redacted]\",\"P256dh\":\"[redacted]\",\"UserAgent\":\"Edge\"}";
        var old = await SeedLogAsync(tag, user: $"u{tag}", changes: created, setup: l => l.EntityType = "PushSubscription");
        var changed = await SeedLogAsync(tag, user: $"u{tag}", changes: modified, setup: l => { l.EntityType = "PushSubscription"; l.Action = "Modified"; });
        var fresh = await SeedLogAsync(tag, user: $"u{tag}", changes: recent, setup: l => l.EntityType = "PushSubscription");
        var other = await SeedLogAsync(tag, user: $"u{tag}", changes: "{\"Auth\":\"not-a-push-key\",\"Title\":\"Ensaio\"}");

        var items = (await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}&pageSize=100")).GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetInt32(), i => i.GetProperty("changes").GetString()!);
        var oldShown = JsonDocument.Parse(items[old]).RootElement;
        foreach (var hidden in new[] { "Auth", "Endpoint", "P256dh" })
        {
            oldShown.GetProperty(hidden).GetString().Should().Be("[redacted]", "the Owner sees that {0} was set, not its value", hidden);
        }

        (oldShown.GetProperty("UserAgent").GetString(), oldShown.GetProperty("UserId").GetString()).Should().Be(("Mozilla/5.0 (Android)", "u-1"));
        items[changed].Should().NotContain("auth-old-secret").And.NotContain("auth-new-secret").And.Contain("\"Auth\":\"[redacted]\"").And.Contain("\"New\":\"b\"");
        items[fresh].Should().Be(recent, "an entry that holds nothing to hide is shown exactly as stored");
        items[other].Should().Contain("not-a-push-key", "the rule is per entity type: other entries keep every field");

        var export = await (await owner.GetAsync($"/api/owner/tracing/export?user=u{tag}")).Content.ReadAsStringAsync();
        export.Should().NotContain("auth-secret-1").And.NotContain("endpoint-secret").And.NotContain("p256-secret").And.NotContain("auth-new-secret")
            .And.Contain("Mozilla/5.0 (Android)").And.Contain("not-a-push-key");

        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().Where(a => a.Id == old || a.Id == changed || a.Id == fresh)
                .OrderBy(a => a.Id).Select(a => a.Changes).ToListAsync()))
            .Should().Equal(new[] { created, modified, recent }, "the stored history is never rewritten");
    }

    [Fact]
    public async Task Tracing_LoginsAndTheAnnouncement()
    {
        var owner = await SignInAsync("Owner");
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        (await AreaHttp.Json(owner, $"/api/owner/tracing/logins?from={today}&to={today}")).GetProperty("records").ValueKind.Should().Be(JsonValueKind.Array);
        (await AreaHttp.Errors(await owner.GetAsync("/api/owner/tracing/logins?from=ontem"))).Keys.Should().Equal("from");

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = " " }))).Keys.Should().Equal("message");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = new string('m', 501) }))).Keys.Should().Equal("message");
        (await owner.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = "O servidor vai reiniciar." })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        // 037: the React shell polls it; anyone with a page open sees it, signed in or not (the Blazor banner's audience).
        var shown = await _factory.CreateClient().GetAsync("/api/announcement");
        shown.StatusCode.Should().Be(HttpStatusCode.OK);
        shown.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = JsonDocument.Parse(await shown.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("message").GetString().Should().Be("O servidor vai reiniciar.");
        body.GetProperty("id").GetInt64().Should().BePositive();
    }

    [Fact]
    public async Task Tracing_Logins_ListEverySignInOfTheRange_NotOnlyTheLatest()
    {
        var owner = await SignInAsync("Owner");
        var tracked = await SeedUserAsync(subscribed: false);
        var latestOnly = await SeedUserAsync(subscribed: false);
        var today = PortugalTime.Today(TimeProvider.System);
        DateTime At(DateTime day, int hour) => PortugalTime.ToUtc(day.AddHours(hour));
        await WithDbAsync(async db =>
        {
            db.LoginEvents.AddRange(
                new LoginEvent { UserId = tracked.Id, At = At(today.AddDays(-1), 9), IsSignIn = true },
                new LoginEvent { UserId = tracked.Id, At = At(today.AddDays(-1), 21), IsSignIn = false },
                new LoginEvent { UserId = tracked.Id, At = At(today, 10), IsSignIn = true },
                new LoginEvent { UserId = tracked.Id, At = At(today.AddDays(-5), 10), IsSignIn = true });
            var stored = await db.Users.SingleAsync(u => u.Id == latestOnly.Id);
            stored.LastLoginDate = At(today.AddDays(-1), 8);
            return await db.SaveChangesAsync();
        });

        var day = (DateTime d) => d.ToString("yyyy-MM-dd");
        var body = await AreaHttp.Json(owner, $"/api/owner/tracing/logins?from={day(today.AddDays(-1))}&to={day(today)}");
        body.GetProperty("historySince").ValueKind.Should().Be(JsonValueKind.String);
        var records = body.GetProperty("records").EnumerateArray().ToDictionary(r => r.GetProperty("userId").GetString()!);

        var t = records[tracked.Id];
        t.GetProperty("fromHistory").GetBoolean().Should().BeTrue();
        t.GetProperty("signIns").GetInt32().Should().Be(2, "the one 5 days ago is outside the range");
        t.GetProperty("activeDays").GetInt32().Should().Be(2);
        t.GetProperty("signInTimes").GetArrayLength().Should().Be(2);
        t.GetProperty("firstAt").GetDateTime().Should().Be(At(today.AddDays(-1), 9));
        t.GetProperty("lastLoginAt").GetDateTime().Should().Be(At(today, 10));

        var l = records[latestOnly.Id];
        l.GetProperty("fromHistory").GetBoolean().Should().BeFalse("listed only by LastLoginDate, as before 050");
        l.GetProperty("signIns").GetInt32().Should().Be(0);
        l.GetProperty("signInTimes").GetArrayLength().Should().Be(0);

        (await AreaHttp.Errors(await owner.GetAsync($"/api/owner/tracing/logins?from={day(today)}&to={day(today.AddDays(-1))}")))
            .Keys.Should().Equal("from");
    }
}
