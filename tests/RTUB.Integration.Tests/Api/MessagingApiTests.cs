using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Messaging v2 on /api/conversations and /api/messages (task 041) through the real host: real login, antiforgery,
/// SQLite and the preserved v1 tables. Push is the recording fake of <see cref="EventsApiFactory"/>: nothing is sent,
/// but every call is seen. Every test makes its own members and conversations.
/// </summary>
public class MessagingApiTests : IClassFixture<EventsApiFactory>
{
    private const string Secret = "segredo-da-tuna-41";
    private readonly EventsApiFactory _factory;

    public MessagingApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing()
    {
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/conversations", "/api/conversations/recipients", "/api/conversations/1" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync("/api/conversations", new { userId = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/conversations/1/messages", new { body = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/conversations/1/read", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/conversations/1/mute", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/messages/1/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/messages/1/reactions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var (a, _) = await SignInAsync(token: false);
        var (_, b) = await SignInAsync();

        (await a.PostAsJsonAsync("/api/conversations", new { userId = b.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await a.PostAsJsonAsync("/api/conversations/1/messages", new { body = "olá" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await a.PostAsJsonAsync("/api/messages/1/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await a.DeleteAsync("/api/messages/1/reactions")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartingAndSending_ReachOnlyTheTwoMembers_AndTheListShowsUnreadUntilRead()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (stranger, _) = await SignInAsync();

        var id = await StartAsync(a, bUser.Id);
        (await StartAsync(b, aUser.Id)).Should().Be(id, "one one-to-one conversation per pair, whoever starts it");

        var sent = await Json(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "  Olá, tudo bem?  " }));
        sent.GetProperty("body").GetString().Should().Be("Olá, tudo bem?", "the text is trimmed");
        sent.GetProperty("isMine").GetBoolean().Should().BeTrue();

        var row = Row(await Json(b, "/api/conversations"), id);
        row.GetProperty("unreadCount").GetInt32().Should().Be(1);
        row.GetProperty("lastMessagePreview").GetString().Should().Be("Olá, tudo bem?");
        row.GetProperty("title").GetString().Should().Be(aUser.Nickname);
        row.GetProperty("canSend").GetBoolean().Should().BeTrue();
        Row(await Json(a, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(0, "my own message is never unread");

        (await b.PostAsync($"/api/conversations/{id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Row(await Json(b, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(0);
        Messages(await Json(a, $"/api/conversations/{id}")).Single().GetProperty("seen").GetBoolean().Should().BeTrue();

        // Somebody else's conversation does not exist for a stranger - not even for reading.
        Ids(await Json(stranger, "/api/conversations")).Should().NotContain(id);
        (await stranger.GetAsync($"/api/conversations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "intruso" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/conversations/{id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/conversations/{id}/mute", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminAndOwner_CannotReadOtherPeoplesConversations()
    {
        var (a, _) = await SignInAsync();
        var (_, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret })).EnsureSuccessStatusCode();

        foreach (var role in new[] { "Admin", "Owner" })
        {
            var (boss, _) = await SignInAsync(role);
            Ids(await Json(boss, "/api/conversations")).Should().NotContain(id, role);
            var response = await boss.GetAsync($"/api/conversations/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, role);
            (await response.Content.ReadAsStringAsync()).Should().NotContain(Secret);
        }
    }

    [Fact]
    public async Task EmptyTooLongAndBadTargets_AreRefused()
    {
        var (a, aUser) = await SignInAsync();
        var (_, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);

        (await Errors(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "   \n\t " }))).Should().ContainKey("body");
        (await Errors(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = (string?)null }))).Should().ContainKey("body");
        (await Errors(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = new string('x', 2001) }))).Should().ContainKey("body");
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = new string('x', 2000) })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Errors(await a.PostAsJsonAsync("/api/conversations", new { userId = aUser.Id }))).Should().ContainKey("userId", "not with yourself");
        (await Errors(await a.PostAsJsonAsync("/api/conversations", new { userId = "nobody" }))).Should().ContainKey("userId");
        (await a.GetAsync("/api/conversations/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Replies_QuoteAMessageOfTheSameConversation()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (_, cUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        var original = await Json(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "Ensaio às 21h?" }));
        var originalId = original.GetProperty("id").GetInt32();

        var reply = await Json(await b.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "Sim!", replyToMessageId = originalId }));
        reply.GetProperty("replyTo").GetProperty("messageId").GetInt32().Should().Be(originalId);
        reply.GetProperty("replyTo").GetProperty("body").GetString().Should().Be("Ensaio às 21h?");
        reply.GetProperty("replyTo").GetProperty("senderName").GetString().Should().Be(aUser.Nickname);
        Messages(await Json(a, $"/api/conversations/{id}")).Last().GetProperty("replyTo").GetProperty("body").GetString().Should().Be("Ensaio às 21h?");

        // A message of another conversation cannot be quoted (it would leak its text).
        var other = await StartAsync(a, cUser.Id);
        var elsewhere = await Json(await a.PostAsJsonAsync($"/api/conversations/{other}/messages", new { body = Secret }));
        var refused = await b.PostAsJsonAsync($"/api/conversations/{id}/messages",
            new { body = "?", replyToMessageId = elsewhere.GetProperty("id").GetInt32() });
        (await Errors(refused)).Should().ContainKey("replyToMessageId");
    }

    [Fact]
    public async Task Reactions_AreOnePerMember_FromTheFixedSet()
    {
        var (a, _) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (stranger, _) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        var messageId = (await Json(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "Vamos?" }))).GetProperty("id").GetInt32();

        var liked = await Json(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "👍" }));
        Reactions(liked).Should().Equal(("👍", 1, true));
        Reactions(Messages(await Json(a, $"/api/conversations/{id}")).Single()).Should().Equal(("👍", 1, false));

        var changed = await Json(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "❤️" }));
        Reactions(changed).Should().Equal(new[] { ("❤️", 1, true) }, "a member has one reaction per message; a new one replaces it");
        await Json(await a.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "❤️" }));
        Reactions(Messages(await Json(b, $"/api/conversations/{id}")).Single()).Should().Equal(("❤️", 2, true));

        (await Errors(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "💩" }))).Should().ContainKey("emoji");
        (await Errors(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "texto livre" }))).Should().ContainKey("emoji");
        (await stranger.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var removed = await Json(await b.DeleteAsync($"/api/messages/{messageId}/reactions"));
        Reactions(removed).Should().Equal(("❤️", 1, false));
    }

    [Fact]
    public async Task Muting_StopsPushForThatMember_AndPushNeverCarriesTheText()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);

        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret })).EnsureSuccessStatusCode();
        var pushes = Pushes(id);
        pushes.Should().ContainSingle();
        pushes[0].Recipients.Should().Equal(bUser.Id);
        pushes[0].Notification.Title.Should().Be("Nova mensagem");
        pushes[0].Notification.Url.Should().Be($"/messages?c={id}");
        (pushes[0].Notification.Title + pushes[0].Notification.Body).Should().NotContain(Secret).And.Contain(aUser.Nickname);

        (await Json(await b.PostAsync($"/api/conversations/{id}/mute", null))).GetProperty("muted").GetBoolean().Should().BeTrue();
        Row(await Json(b, "/api/conversations"), id).GetProperty("muted").GetBoolean().Should().BeTrue();
        Row(await Json(a, "/api/conversations"), id).GetProperty("muted").GetBoolean().Should().BeFalse("muting is per member");
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "outra" })).EnsureSuccessStatusCode();
        Pushes(id).Should().HaveCount(1, "a muted member gets no push");
        Row(await Json(b, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(2, "muted, still unread");

        (await Json(await b.PostAsync($"/api/conversations/{id}/unmute", null))).GetProperty("muted").GetBoolean().Should().BeFalse();
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "terceira" })).EnsureSuccessStatusCode();
        Pushes(id).Should().HaveCount(2);
    }

    [Fact]
    public async Task SystemConversations_AreReadOnly_AndAnnouncementChannels_TakeOnlyTheirSenders()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (owner, ownerUser) = await SignInAsync("Owner");

        var system = await SeedAsync(new Conversation { Participants = aUser.Id, IsSystemConversation = true, Title = "Sistema RTUB" },
            new Message { Body = "Novo ensaio marcado", IsSystem = true, Link = "https://rtub.example/rehearsals/3" });
        var detail = await Json(a, $"/api/conversations/{system}");
        detail.GetProperty("conversation").GetProperty("canSend").GetBoolean().Should().BeFalse();
        detail.GetProperty("conversation").GetProperty("kind").GetString().Should().Be("system");
        Messages(detail).Single().GetProperty("link").GetString().Should().Be("/rehearsals/3", "only this site's path is kept");
        (await a.PostAsJsonAsync($"/api/conversations/{system}/messages", new { body = "olá" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var systemMessage = Messages(detail).Single().GetProperty("id").GetInt32();
        (await a.PostAsJsonAsync($"/api/messages/{systemMessage}/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var channel = await SeedAsync(new Conversation
        {
            Participants = $"{aUser.Id};{bUser.Id};{ownerUser.Id}", IsGroup = true, IsAnnouncementOnly = true, Title = "ANÚNCIOS",
            CreatedByUserId = "system",
        });
        Row(await Json(b, "/api/conversations"), channel).GetProperty("canSend").GetBoolean().Should().BeFalse();
        (await b.PostAsJsonAsync($"/api/conversations/{channel}/messages", new { body = "posso?" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PostAsJsonAsync($"/api/conversations/{channel}/messages", new { body = "Aviso do Owner" })).StatusCode.Should().Be(HttpStatusCode.OK);

        await GiveCurrentPositionAsync(bUser.Id, Position.Magister);
        (await b.PostAsJsonAsync($"/api/conversations/{channel}/messages", new { body = "Aviso do Magister" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var names = Messages(await Json(a, $"/api/conversations/{channel}")).Select(m => m.GetProperty("sender").GetProperty("id").GetString());
        names.Should().Equal(ownerUser.Id, bUser.Id);
    }

    [Fact]
    public async Task Groups_ShowWhoWrote_ArchivedOnesStayHidden_AndAChatWithARemovedMemberIsClosed()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (outsider, _) = await SignInAsync();

        var group = await SeedAsync(new Conversation { Participants = $"{aUser.Id};{bUser.Id}", IsGroup = true, Title = "NAIPE" });
        (await b.PostAsJsonAsync($"/api/conversations/{group}/messages", new { body = "Olá naipe" })).EnsureSuccessStatusCode();
        var detail = await Json(a, $"/api/conversations/{group}");
        detail.GetProperty("conversation").GetProperty("title").GetString().Should().Be("NAIPE");
        detail.GetProperty("participants").EnumerateArray().Select(p => p.GetProperty("id").GetString()).Should().Equal(bUser.Id);
        Messages(detail).Single().GetProperty("sender").GetProperty("displayName").GetString().Should().Be(bUser.Nickname);
        (await outsider.GetAsync($"/api/conversations/{group}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var archived = await SeedAsync(new Conversation { Participants = $"{aUser.Id};{bUser.Id}", IsGroup = true, Title = "VELHO", IsArchived = true });
        Ids(await Json(a, "/api/conversations")).Should().NotContain(archived);
        (await a.GetAsync($"/api/conversations/{archived}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (_, goneUser) = await SignInAsync();
        var withGone = await StartAsync(a, goneUser.Id);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var gone = await users.FindByIdAsync(goneUser.Id);
            gone!.IsExpelled = true;
            (await users.UpdateAsync(gone)).Succeeded.Should().BeTrue();
        }

        Row(await Json(a, "/api/conversations"), withGone).GetProperty("canSend").GetBoolean().Should().BeFalse();
        (await a.PostAsJsonAsync($"/api/conversations/{withGone}/messages", new { body = "olá?" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(a, "/api/conversations/recipients")).EnumerateArray().Select(p => p.GetProperty("id").GetString())
            .Should().NotContain(goneUser.Id).And.NotContain(aUser.Id).And.Contain(bUser.Id);
    }

    [Fact]
    public async Task OlderMessages_ComeInPages()
    {
        var (a, aUser) = await SignInAsync();
        var (_, bUser) = await SignInAsync();
        var conversation = new Conversation { Participants = $"{aUser.Id};{bUser.Id}" };
        var id = await SeedAsync(conversation, Enumerable.Range(1, 60).Select(i => new Message { SenderId = bUser.Id, Body = $"m{i}" }).ToArray());

        var first = await Json(a, $"/api/conversations/{id}");
        first.GetProperty("hasMore").GetBoolean().Should().BeTrue();
        var page = Messages(first).Select(m => m.GetProperty("body").GetString()).ToList();
        page.Should().HaveCount(50).And.StartWith("m11").And.EndWith("m60");

        var before = Messages(first).First().GetProperty("id").GetInt32();
        var older = await Json(a, $"/api/conversations/{id}?before={before}");
        older.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        Messages(older).Select(m => m.GetProperty("body").GetString()).Should().Equal(Enumerable.Range(1, 10).Select(i => $"m{i}"));
    }

    [Fact]
    public async Task Tracing_NeverRecordsMessagingContent()
    {
        var (a, _) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        var messageId = (await Json(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret }))).GetProperty("id").GetInt32();
        await Json(await b.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret + "-reply", replyToMessageId = messageId }));
        await Json(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "😂" }));
        await Json(await b.PostAsync($"/api/conversations/{id}/mute", null));
        (await b.PostAsync($"/api/conversations/{id}/read", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        logs.Select(l => l.EntityType).Should().NotContain(new[] { "Message", "Conversation", "MessageReaction", "ConversationUserSettings" });
        logs.Should().NotContain(l => (l.Changes ?? "").Contains(Secret) || (l.EntityDisplayName ?? "").Contains(Secret));
    }

    // ---------- helpers ----------

    private async Task<int> StartAsync(HttpClient client, string userId) =>
        (await Json(await client.PostAsJsonAsync("/api/conversations", new { userId }))).GetProperty("conversation").GetProperty("id").GetInt32();

    private async Task<int> SeedAsync(Conversation conversation, params Message[] messages)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        conversation.LastMessageAt = DateTime.UtcNow;
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        foreach (var message in messages)
        {
            message.ConversationId = conversation.Id;
            db.Messages.Add(message);
            await db.SaveChangesAsync(); // one at a time, so ids follow the order
        }

        return conversation.Id;
    }

    private async Task GiveCurrentPositionAsync(string userId, Position position)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var start = now.Month >= 9 ? now.Year : now.Year - 1;
        db.RoleAssignments.Add(RoleAssignment.Create(userId, position, start, start + 1));
        await db.SaveChangesAsync();
    }

    private List<(IReadOnlyList<string> Recipients, SendPushNotificationDto Notification)> Pushes(int conversationId) =>
        _factory.Push.Invocations
            .Where(i => i.Method.Name == "SendToSelectedUsersAsync")
            .Select(i => (Recipients: (IReadOnlyList<string>)((IEnumerable<string>)i.Arguments[0]).ToList(), Notification: (SendPushNotificationDto)i.Arguments[1]))
            .Where(p => p.Notification.Tag == $"message-{conversationId}")
            .ToList();

    private static JsonElement Row(JsonElement list, int id) =>
        list.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == id);

    private static IEnumerable<int> Ids(JsonElement list) => list.EnumerateArray().Select(c => c.GetProperty("id").GetInt32());

    private static List<JsonElement> Messages(JsonElement detail) => detail.GetProperty("messages").EnumerateArray().ToList();

    private static List<(string, int, bool)> Reactions(JsonElement message) =>
        message.GetProperty("reactions").EnumerateArray()
            .Select(r => (r.GetProperty("emoji").GetString()!, r.GetProperty("count").GetInt32(), r.GetProperty("mine").GetBoolean()))
            .ToList();

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static int _ip;

    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null, bool token = true)
    {
        var n = Interlocked.Increment(ref _ip);
        var session = await CookieTestSession.SignInAsync(_factory, $"msg{Guid.NewGuid():N}"[..20], $"10.47.{n / 250}.{n % 250 + 1}", role);
        if (token)
        {
            await WithTokenAsync(session.Client);
        }

        return session;
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.48.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
