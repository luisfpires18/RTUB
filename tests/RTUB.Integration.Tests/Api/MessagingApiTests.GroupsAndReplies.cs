// MessagingApiTests: GroupsAndReplies tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

public partial class MessagingApiTests
{
    [Fact]
    public async Task AGroup_StartsWithSeveralMembersAndAName_AndReachesThemAll()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (c, cUser) = await SignInAsync();
        var (stranger, _) = await SignInAsync();

        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id, cUser.Id }, body = "olá" })))
            .Should().ContainKey("title", "a group needs a name");
        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id, cUser.Id }, title = new string('x', 201), body = "olá" })))
            .Should().ContainKey("title");
        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id, "nobody" }, title = "Naipe", body = "olá" })))
            .Should().ContainKey("userIds");

        var started = await AreaHttp.Json(await a.PostAsJsonAsync("/api/conversations",
            new { userIds = new[] { bUser.Id, cUser.Id, bUser.Id, aUser.Id }, title = "  Naipe de bandolins  ", body = "Ensaio extra?" }));
        var id = started.GetProperty("conversation").GetProperty("id").GetInt32();
        started.GetProperty("conversation").GetProperty("kind").GetString().Should().Be("group");
        started.GetProperty("conversation").GetProperty("title").GetString().Should().Be("Naipe de bandolins");
        started.GetProperty("participants").GetArrayLength().Should().Be(2, "the other members, each once");

        foreach (var member in new[] { b, c })
        {
            var row = Row(await AreaHttp.Json(member, "/api/conversations"), id);
            row.GetProperty("unreadCount").GetInt32().Should().Be(1);
            row.GetProperty("canSend").GetBoolean().Should().BeTrue();
        }

        (await c.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "Sim" })).EnsureSuccessStatusCode();
        Pushes(id).SelectMany(p => p.Recipients).Should().BeEquivalentTo(new[] { bUser.Id, cUser.Id, aUser.Id, bUser.Id });
        Ids(await AreaHttp.Json(stranger, "/api/conversations")).Should().NotContain(id);
        (await AreaHttp.Json(a, $"/api/conversations/direct/{bUser.Id}")).GetProperty("id").ValueKind.Should().Be(JsonValueKind.Null, "a group is not a one-to-one chat");
    }

    [Fact]
    public async Task RemovingAChat_IsForOneToOneOnly_HidesItForBoth_AndANewMessageBringsItBack()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (stranger, _) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);

        (await stranger.DeleteAsync($"/api/conversations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await a.DeleteAsync($"/api/conversations/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Ids(await AreaHttp.Json(a, "/api/conversations")).Should().NotContain(id);
        Ids(await AreaHttp.Json(b, "/api/conversations")).Should().NotContain(id, "v1's delete: archived for both");

        (await StartAsync(b, aUser.Id, "Voltei")).Should().Be(id, "writing again brings the same chat back");
        Messages(await AreaHttp.Json(a, $"/api/conversations/{id}")).Should().HaveCount(2, "the old messages are kept");

        var group = await SeedAsync(new Conversation { Participants = $"{aUser.Id};{bUser.Id}", IsGroup = true, Title = "NAIPE" });
        var system = await SeedAsync(new Conversation { Participants = aUser.Id, IsSystemConversation = true, Title = "Notificações RTUB" });
        (await a.DeleteAsync($"/api/conversations/{group}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await a.DeleteAsync($"/api/conversations/{system}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Ids(await AreaHttp.Json(a, "/api/conversations")).Should().Contain(new[] { group, system });
    }

    [Fact]
    public async Task Replies_QuoteAMessageOfTheSameConversation()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (_, cUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        var original = await AreaHttp.Json(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "Ensaio às 21h?" }));
        var originalId = original.GetProperty("id").GetInt32();

        var reply = await AreaHttp.Json(await b.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "Sim!", replyToMessageId = originalId }));
        reply.GetProperty("replyTo").GetProperty("messageId").GetInt32().Should().Be(originalId);
        reply.GetProperty("replyTo").GetProperty("body").GetString().Should().Be("Ensaio às 21h?");
        reply.GetProperty("replyTo").GetProperty("senderName").GetString().Should().Be(aUser.Nickname);
        Messages(await AreaHttp.Json(a, $"/api/conversations/{id}")).Last().GetProperty("replyTo").GetProperty("body").GetString().Should().Be("Ensaio às 21h?");

        // A message of another conversation cannot be quoted (it would leak its text).
        var other = await StartAsync(a, cUser.Id);
        var elsewhere = await AreaHttp.Json(await a.PostAsJsonAsync($"/api/conversations/{other}/messages", new { body = Secret }));
        var refused = await b.PostAsJsonAsync($"/api/conversations/{id}/messages",
            new { body = "?", replyToMessageId = elsewhere.GetProperty("id").GetInt32() });
        (await AreaHttp.Errors(refused)).Should().ContainKey("replyToMessageId");
    }

    [Fact]
    public async Task Reactions_AreOnePerMember_FromTheFixedSet()
    {
        var (a, _) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (stranger, _) = await SignInAsync();
        var started = await AreaHttp.Json(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id }, body = "Vamos?" }));
        var id = started.GetProperty("conversation").GetProperty("id").GetInt32();
        var messageId = Messages(started).Single().GetProperty("id").GetInt32();

        var liked = await AreaHttp.Json(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "👍" }));
        Reactions(liked).Should().Equal(("👍", 1, true));
        Reactions(Messages(await AreaHttp.Json(a, $"/api/conversations/{id}")).Single()).Should().Equal(("👍", 1, false));

        var changed = await AreaHttp.Json(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "❤️" }));
        Reactions(changed).Should().Equal(new[] { ("❤️", 1, true) }, "a member has one reaction per message; a new one replaces it");
        await AreaHttp.Json(await a.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "❤️" }));
        Reactions(Messages(await AreaHttp.Json(b, $"/api/conversations/{id}")).Single()).Should().Equal(("❤️", 2, true));

        (await AreaHttp.Errors(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "💩" }))).Should().ContainKey("emoji");
        (await AreaHttp.Errors(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "texto livre" }))).Should().ContainKey("emoji");
        (await stranger.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var removed = await AreaHttp.Json(await b.DeleteAsync($"/api/messages/{messageId}/reactions"));
        Reactions(removed).Should().Equal(("❤️", 1, false));
    }
}
